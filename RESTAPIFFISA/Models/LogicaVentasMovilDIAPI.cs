using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;

namespace RESTAPIFFISA
{
    public class LogicaVentasMovilDIAPI
    {
        #region variables
        public AccesoDatosVentas AD = new AccesoDatosVentas();
        public LoginServiceLayer LoginService = new LoginServiceLayer();
        public GlobalCommands GlobalCommands = new GlobalCommands();
        private static readonly ILog log = LogManager.GetLogger(typeof(LogicaVentasMovil));
        private string EmailsFacturacion { get; }
        private string EmailsVentas { get; }

        #endregion

        #region constructor
        public LogicaVentasMovilDIAPI() //se ejecuta al instanciar la clase
        {
            try
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("Code", "FacturacionM");
                EmailsFacturacion = GlobalCommands.ExecuteProcedure(GlobalCommands.GCGetEmailAutorizacionesHHOC, parameters);
                parameters.Clear();
                parameters.Add("Code", "Ventas");
                EmailsVentas = GlobalCommands.ExecuteProcedure(GlobalCommands.GCGetEmailAutorizacionesHHOC, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                EmailsFacturacion = "";
                EmailsVentas = "";
            }
        }
        #endregion

        #region SBO VENTAS ENTREGAS
        public async Task<GlobalCommands.SapResponse> CreateDeliveryDraftDIAsync(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            var response = new GlobalCommands.SapResponse() { IsError = true };

            Company oCompany = null;

            try
            {
                // -----------------------------
                // PASO 1: Obtener la orden desde Service Layer
                // -----------------------------
                var urlOrder = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({RequestData.OrdenVenta})";
                var orderResponse = await LoginService._httpClient.GetAsync(urlOrder);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    var error = await orderResponse.Content.ReadAsStringAsync();
                    response.Message = $"Error al obtener la orden: {error}";
                    return response;
                }

                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                dynamic orderData = Newtonsoft.Json.JsonConvert.DeserializeObject(orderJson);

                // -----------------------------
                // PASO 2: Preparar cabecera
                // -----------------------------
                var headerDict = HeaderDocumentDeliveryDIAPI(orderData, RequestData);

                // -----------------------------
                // PASO 3: Obtener lotes
                // -----------------------------
                Dictionary<string, string> keys = new Dictionary<string, string>
                {
                    { "Folio", RequestData.FolioEM },
                    { "Usuario", RequestData.Usuario }
                };
                string lotes = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosVentasHHEMOV, keys);

                if (lotes == "[]")
                {
                    response.Message = $"No se encontró información de lotes asociada al folio {RequestData.FolioEM}";
                    return response;
                }
                else if (lotes.Contains("Error"))
                {
                    response.Message = $"No fue posible obtener información de lotes: {lotes}";
                    return response;
                }

                JArray LotesData = JArray.Parse(lotes);

                // -----------------------------
                // PASO 4: Obtener líneas y convertir correctamente
                // -----------------------------
                var dictWithLines = await LinesDocumentDeliveryDIAPI(headerDict, LotesData, orderData, RequestData);
                var linesList = ((IEnumerable<object>)dictWithLines["DocumentLines"])
                                .Cast<IDictionary<string, object>>() // ✅ Ahora funcionará perfectamente
                                .ToList();

                // -----------------------------
                // PASO 5: Conectar con SAP DI API
                // -----------------------------
                oCompany = new Company
                {
                    Server = ConfigurationManager.AppSettings["SapServer"],
                    CompanyDB = ConfigurationManager.AppSettings["SapDatabase"],
                    UserName = ConfigurationManager.AppSettings["SapUser"],
                    Password = ConfigurationManager.AppSettings["SapPassword"],
                    DbServerType = BoDataServerTypes.dst_MSSQL2019,
                    UseTrusted = false
                };

                int lRetCode = oCompany.Connect();
                if (lRetCode != 0)
                {
                    int errCode;
                    string errMsg;
                    oCompany.GetLastError(out errCode, out errMsg);
                    response.Message = $"Error conectando a DI API: {errCode} {errMsg}";
                    return response;
                }

                // -----------------------------
                // PASO 6: Crear objeto borrador
                // -----------------------------
                Documents draft = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oDrafts);
                draft.DocObjectCode = (BoObjectTypes)15;

                // -----------------------------
                // PASO 7: Cargar cabecera
                // -----------------------------
                // ✅ Asignar campos directamente en lugar de usar reflexión
                draft.CardCode = headerDict.ContainsKey("CardCode") ? headerDict["CardCode"].ToString() : "";
                draft.DocDate = headerDict.ContainsKey("DocDate") ? DateTime.Parse(headerDict["DocDate"].ToString()) : DateTime.Now;
                try
                {
                    draft.DocDueDate = headerDict.ContainsKey("DocDueDate") ? DateTime.Parse(headerDict["DocDueDate"].ToString()) : DateTime.Now;
                }
                catch
                {
                    draft.DocDueDate = DateTime.Now;
                }
                draft.NumAtCard = headerDict.ContainsKey("NumAtCard") ? headerDict["NumAtCard"].ToString() : "";
                draft.Comments = headerDict.ContainsKey("Comments") ? headerDict["Comments"].ToString() : "";

                // Campos opcionales con validación
                if (headerDict.ContainsKey("SalesPersonCode") && headerDict["SalesPersonCode"] != null)
                    draft.SalesPersonCode = Convert.ToInt32(headerDict["SalesPersonCode"]);

                if (headerDict.ContainsKey("ContactPersonCode") && headerDict["ContactPersonCode"] != null)
                    draft.ContactPersonCode = Convert.ToInt32(headerDict["ContactPersonCode"]);

                if (headerDict.ContainsKey("PaymentGroupCode") && headerDict["PaymentGroupCode"] != null)
                    draft.PaymentGroupCode = Convert.ToInt32(headerDict["PaymentGroupCode"]);

                if (headerDict.ContainsKey("DocCurrency") && headerDict["DocCurrency"] != null)
                    draft.DocCurrency = headerDict["DocCurrency"].ToString();

                if (headerDict.ContainsKey("DocRate") && headerDict["DocRate"] != null)
                    draft.DocRate = Convert.ToDouble(headerDict["DocRate"]);

                // ✅ Asignar UDFs (User Defined Fields)
                foreach (var kvp in headerDict)
                {
                    if (kvp.Key.StartsWith("U_"))
                    {
                        try
                        {
                            draft.UserFields.Fields.Item(kvp.Key).Value = kvp.Value;
                        }
                        catch (Exception ex)
                        {
                            // Log del UDF que falló (opcional)
                            Console.WriteLine($"No se pudo asignar UDF {kvp.Key}: {ex.Message}");
                        }
                    }
                }

                // PASO 8: Cargar líneas y lotes
                foreach (var line in linesList)
                {
                    // ✅ Campos estándar de la línea
                    draft.Lines.ItemCode = line["ItemCode"].ToString();
                    draft.Lines.Quantity = Convert.ToDouble(line["Quantity"]);

                    // ✅ IMPORTANTE: Enviar UnitPrice
                    if (line.ContainsKey("UnitPrice"))
                        draft.Lines.UnitPrice = Convert.ToDouble(line["UnitPrice"]);

                    // ✅ Enviar DiscountPercent (puede ser negativo)
                    if (line.ContainsKey("DiscountPercent"))
                        draft.Lines.DiscountPercent = Convert.ToDouble(line["DiscountPercent"]);

                    draft.Lines.WarehouseCode = line["WarehouseCode"].ToString();

                    // ✅ Campos base (vincular con OV)
                    if (line.ContainsKey("BaseType"))
                        draft.Lines.BaseType = Convert.ToInt32(line["BaseType"]);

                    if (line.ContainsKey("BaseEntry"))
                        draft.Lines.BaseEntry = Convert.ToInt32(line["BaseEntry"]);

                    if (line.ContainsKey("BaseLine"))
                        draft.Lines.BaseLine = Convert.ToInt32(line["BaseLine"]);

                    // ✅ Asignar UDFs de la línea
                    foreach (var kvp in line)
                    {
                        if (kvp.Key.StartsWith("U_"))
                        {
                            try
                            {
                                draft.Lines.UserFields.Fields.Item(kvp.Key).Value = kvp.Value;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"No se pudo asignar UDF de línea {kvp.Key}: {ex.Message}");
                            }
                        }
                    }

                    // ✅ Asignar lotes
                    if (line.ContainsKey("BatchNumbers"))
                    {
                        var batches = (IEnumerable<object>)line["BatchNumbers"];
                        bool firstBatch = true;

                        foreach (var batchObj in batches)
                        {
                            dynamic batch = batchObj;

                            if (!firstBatch)
                            {
                                draft.Lines.BatchNumbers.Add();
                            }

                            draft.Lines.BatchNumbers.BatchNumber = batch.BatchNumber;
                            draft.Lines.BatchNumbers.Quantity = Convert.ToDouble(batch.Quantity);

                            firstBatch = false;
                        }
                    }

                    // ✅ Agregar la línea al documento
                    draft.Lines.Add();
                }

                // -----------------------------
                // PASO 9: Agregar borrador en SAP
                // -----------------------------
                if (draft.Add() != 0)
                {
                    int errCode;
                    string errMsg;
                    oCompany.GetLastError(out errCode, out errMsg);
                    response.Message = $"Error creando borrador en DI API: {errCode} {errMsg}";
                    return response;
                }

                // -----------------------------
                // PASO 10: Obtener número de documento creado
                // -----------------------------
                int docEntry = int.Parse(oCompany.GetNewObjectKey());
                response.IsError = false;
                response.Message = $"Borrador de entrega creado correctamente. DocEntry: {docEntry}";
                response.OrdenVenta = RequestData.OrdenVenta.ToString();
            }
            catch (Exception ex)
            {
                response.Message = $"Excepción: {ex.Message}";
            }
            finally
            {
                if (oCompany != null && oCompany.Connected)
                    oCompany.Disconnect();
            }

            return response;
        }
        // Método para crear BORRADOR de entrega desde orden (totalmente refactorizado)
        public async Task<GlobalCommands.SapResponse> CreateDeliveryDraftAutomaticFromOrderAsync(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };

            try
            {
                // -----------------------------
                // PASO 1: Obtener orden de venta (REUTILIZABLE)
                // -----------------------------
                var urlOrder = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({RequestData.OrdenVenta})";
                var orderResponse = await LoginService._httpClient.GetAsync(urlOrder);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    var error = await orderResponse.Content.ReadAsStringAsync();
                    responseAbx.Message = $"Error al obtener la orden: {error}";
                    return responseAbx;
                }

                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                dynamic orderData = JsonConvert.DeserializeObject(orderJson);

                // -----------------------------
                // PASO 2: Preparar objeto de borrador (ENCABEZADO DOCUMENTO)
                // -----------------------------
                dynamic deliveryDraft = new ExpandoObject();
                var dictDraft = (IDictionary<string, object>)deliveryDraft;

                // Agregar código específico para borrador
                dictDraft["DocObjectCode"] = "15"; // Código SAP para entrega (draft)

                // REUTILIZAR método de cabecera
                var headerFields = HeaderDocumentDelivery(orderData, RequestData);
                foreach (var field in headerFields)
                {
                    dictDraft[field.Key] = field.Value;
                }

                // -----------------------------
                // PASO 2a: Obtener lotes escaneados (REUTILIZABLE)
                // -----------------------------
                Dictionary<string, string> keys = new Dictionary<string, string>
                {
                    { "Folio", RequestData.FolioEM },
                    { "Usuario", RequestData.Usuario }
                };
                string lotes = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosVentasHHEMOV, keys);

                if (lotes == "[]")
                {
                    responseAbx.Message = $"No se encontró información de lotes asociada al folio {RequestData.FolioEM}";
                    return responseAbx;
                }
                else if (lotes.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener información de lotes asociada al folio {RequestData.FolioEM}: {lotes}";
                    return responseAbx;
                }

                JArray LotesData = JArray.Parse(lotes);

                // -----------------------------
                // PASO 2b: Agregar líneas del documento (REUTILIZABLE)
                // -----------------------------
                try
                {
                    dictDraft = await LinesDocumentDelivery(dictDraft, LotesData, orderData, RequestData);
                }
                catch (Exception E)
                {
                    responseAbx.Message = E.Message;
                    return responseAbx;
                }

                // -----------------------------
                // PASO 3: Verificar diferencias (ESPECÍFICO PARA BORRADOR)
                // -----------------------------
                var DiferenciaLotes = new Dictionary<string, string>();
                foreach (var lote in LotesData)
                {
                    double CantidadReferencia = (double)(lote["CantidadReferencia"].ToString() == string.Empty ? 0 : lote["CantidadReferencia"]);
                    double CantidadLote = (double)(lote["Cantidad"].ToString() == string.Empty ? 0 : lote["Cantidad"]);
                    double diferenciaLote = Math.Abs(CantidadReferencia - CantidadLote);

                    if (diferenciaLote > 0 && CantidadReferencia > 0)
                        DiferenciaLotes[(string)lote["Lote"]] = diferenciaLote.ToString();
                }

                if (DiferenciaLotes.Count > 0)
                {
                    StringBuilder ConsideracionesLote = new StringBuilder();
                    foreach (var diferencia in DiferenciaLotes)
                    {
                        ConsideracionesLote.AppendLine($"⚠️ Existe una diferencia de cantidad en el lote: {diferencia.Key} de {diferencia.Value} unidades respecto a la cantidad de referencia.");
                    }

                    NotificacionFacturacion(
                        $"Notificación de diferencias de cantidad en lote para la orden de venta {RequestData.OrdenVenta}",
                        $"Notificación de consideraciones para la orden de venta {RequestData.OrdenVenta} sobre la diferencia de cantidad en el lote",
                        $"🗒️ Hola, se deben tomar en cuenta las siguientes consideraciones sobre la diferencia de cantidad en el lote:",
                        ConsideracionesLote.ToString(),
                        RequestData.OrdenVenta.ToString(),
                        "Facturación"
                    );
                }

                // -----------------------------
                // PASO 4: Enviar borrador a SAP
                // -----------------------------
                var deliveryUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Drafts";
                var jsonBody = JsonConvert.SerializeObject(dictDraft);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(deliveryUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    responseAbx.IsError = false;
                    string DocNum = created.DocNum;
                    string DocEntry = created.DocEntry;
                    responseAbx.Message = $"Borrador de entrega creado exitosamente. Documento SAP: {DocNum}" + Environment.NewLine + Environment.NewLine + "● Cantidad de rollos comprendidos en el documento preeliminar de entrega: " + RequestData.Rollos;


                    // -----------------------------
                    // ACTUALIZAR LOTES (REUTILIZABLE con modificación)
                    // -----------------------------
                    //UpdateLotesEntregaDraft(LotesData, orderData, RequestData);

                    // -----------------------------
                    // ACTUALIZAR DOCUMENTO (REUTILIZABLE con modificación)
                    // -----------------------------
                    //UpdateDocumentDraft(RequestData, DocNum);

                    // -----------------------------
                    // NOTIFICAR A WEB (REUTILIZABLE)
                    // -----------------------------
                    //await NotifyWebQuanqityRollos(RequestData);

                    // -----------------------------
                    // ACTUALIZAR MONTO EN LETRAS USANDO STORED PROCEDURE
                    // -----------------------------
                    try
                    {
                        await UpdateMontoLetrasDraft(RequestData, DocEntry);
                    }
                    catch (Exception E)
                    {
                        responseAbx.Message = "No fue posible actualizar el monto en letras: " + E.Message;
                        return responseAbx;
                    }
                }
                else
                {
                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;
                        responseAbx.Message = $"{errorCode} {errorMessage}";
                        responseAbx.OrdenVenta = (string)orderData.DocNum;
                    }
                    catch
                    {
                        responseAbx.Message = $"{postResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            return responseAbx;
        }
        // Método para crear entrega desde orden (adaptado para MVC) copiando todos los campos definidos
        public async Task<GlobalCommands.SapResponse> CreateDeliveryFromOrderAutomaticAsync(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };

            try
            {
                // -----------------------------
                // PASO 1: Obtener orden de venta
                // -----------------------------
                var urlOrder = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({RequestData.OrdenVenta})";
                var orderResponse = await LoginService._httpClient.GetAsync(urlOrder);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    var error = await orderResponse.Content.ReadAsStringAsync();
                    responseAbx.Message = $"Error al obtener la orden: {error}";
                    return responseAbx;
                }

                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                log.Info($"Orden completa: {orderJson}");
                dynamic orderData = JsonConvert.DeserializeObject(orderJson);

                // -----------------------------
                // PASO 2: Preparar objeto de entrega (ENCABEZADO DOCUMENTO)
                // -----------------------------
                dynamic delivery = new ExpandoObject();
                var dictDelivery = (IDictionary<string, object>)delivery;
                dictDelivery = HeaderDocumentDelivery(orderData, RequestData);

                // -----------------------------
                // PASO 2a: Obtener lotes escaneados (LINEAS DOCUMENTO)
                // -----------------------------
                Dictionary<string, string> keys = new Dictionary<string, string>
                {
                    { "Folio", RequestData.FolioEM },
                    { "Usuario", RequestData.Usuario }
                };
                string lotes = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosVentasHHEMOV, keys);

                if (lotes == "[]")
                {
                    responseAbx.Message = $"No se encontró información de lotes asociada al folio {RequestData.FolioEM}";
                    return responseAbx;
                }
                else if (lotes.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener información de lotes asociada al folio {RequestData.FolioEM}: {lotes}";
                    return responseAbx;
                }

                JArray LotesData = JArray.Parse(lotes);

                // Agregar líneas del documento
                try
                {
                    dictDelivery = await LinesDocumentDelivery(dictDelivery, LotesData, orderData, RequestData);
                }
                catch (Exception E)
                {
                    responseAbx.Message = E.Message;
                    return responseAbx;
                }

                // -----------------------------
                // PASO 3: Enviar entrega a SAP
                // -----------------------------
                var deliveryUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes";
                var jsonBody = JsonConvert.SerializeObject(dictDelivery);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(deliveryUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    responseAbx.IsError = false;
                    string DocNum = created.DocNum;
                    string DocEntry = created.DocEntry;
                    responseAbx.Message = $"Entrega creada exitosamente. Documento SAP: {DocNum}" + Environment.NewLine + Environment.NewLine + "● Cantidad de rollos comprendidos en la entrega: " + RequestData.Rollos;

                    // -----------------------------
                    // ACTUALIZAR LOTES
                    // -----------------------------
                    UpdateLotesEntrega(LotesData, orderData, RequestData);

                    // -----------------------------
                    // ACTUALIZAR DOCUMENTO
                    // -----------------------------
                    UpdateDocumentDelivery(RequestData, DocNum);

                    // -----------------------------
                    // NOTIFICACIONES
                    // -----------------------------
                    NotificacionFacturacion(
                        $"Notificación de entrega pendiente de factura para la entrega: {DocNum}",
                        $"Notificación de entrega pendiente de factura para la entrega: {DocNum}",
                        $"📝 Hola, se ha generado una entrega de mercancía para la orden de venta: {(string)orderData.DocNum}",
                        "Considerar la entrega de factura.",
                        (string)orderData.DocNum,
                        "Facturación"
                    );

                    // -----------------------------
                    // NOTIFICAR A WEB
                    // -----------------------------
                    await NotifyWebQuanqityRollos(RequestData);

                    // -----------------------------
                    // ACTUALIZAR MONTO EN LETRAS USANDO STORED PROCEDURE
                    // -----------------------------
                    try
                    {
                        await UpdateMontoLetras(RequestData, DocEntry);
                    }
                    catch (Exception E)
                    {
                        responseAbx.Message = "No fue posible actualizar el monto en letras: " + E.Message;
                        return responseAbx;
                    }
                }
                else
                {
                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;
                        responseAbx.Message = $"{errorCode} {errorMessage}";
                        responseAbx.OrdenVenta = (string)orderData.DocNum;
                    }
                    catch
                    {
                        responseAbx.Message = $"{postResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            return responseAbx;
        }
        private decimal CalcularDiscountPercentExacto(decimal cantidad, decimal precioUnitario, decimal totalDeseado)
        {
            // Calcula el descuento exacto
            decimal discountPercent = 100 * (1 - (totalDeseado / (cantidad * precioUnitario)));

            // Ajuste para evitar decimales residuales en SAP
            discountPercent = Math.Round(discountPercent, 8);

            return discountPercent;
        }
        private IDictionary<string, object> HeaderDocumentDeliveryDIAPI(dynamic orderData, AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            // ✅ CAMBIO AQUÍ: Usar Dictionary en lugar de ExpandoObject
            var dictDelivery = new Dictionary<string, object>();

            // Campos estándar
            dictDelivery["CardCode"] = (string)orderData.CardCode;
            dictDelivery["DocDate"] = RequestData.FechaEntrega.ToString("yyyy-MM-dd");
            dictDelivery["DocDueDate"] = (string)orderData.DocDueDate;
            dictDelivery["NumAtCard"] = (string)orderData.NumAtCard;
            dictDelivery["Comments"] = (string)orderData.Comments + " Basado en Pedidos de cliente: " + (string)orderData.DocNum + Environment.NewLine + Environment.NewLine + " Documento Creado Por Interfáz FFISA " + DateTime.Now.ToString();
            dictDelivery["SalesPersonCode"] = (int?)orderData.SalesPersonCode;
            dictDelivery["ContactPersonCode"] = (int?)orderData.ContactPersonCode;
            dictDelivery["PaymentGroupCode"] = (int?)orderData.PaymentGroupCode;
            dictDelivery["DocCurrency"] = (string)orderData.DocCurrency;
            dictDelivery["DocRate"] = (double?)orderData.DocRate;

            // Copiar automáticamente todos los UDFs de cabecera
            foreach (JProperty prop in ((JObject)orderData).Properties())
            {
                if (prop.Name.StartsWith("U_"))
                {
                    object value = null;
                    if (prop.Value.Type == JTokenType.Integer)
                        value = (int)prop.Value;
                    else if (prop.Value.Type == JTokenType.Float)
                        value = (double)prop.Value;
                    else if (prop.Value.Type == JTokenType.Boolean)
                        value = (bool)prop.Value;
                    else
                        value = prop.Value?.ToString();

                    // ✅ Solo copiar si tiene un valor no vacío
                    if (value != null && !(value is string s && string.IsNullOrWhiteSpace(s)))
                    {
                        dictDelivery[prop.Name] = value;
                    }
                }
            }

            // Agregar manualmente usuario y comentarios
            dictDelivery["U_ComHanheld"] = RequestData.Comentarios;
            dictDelivery["U_UsuIniSes"] = RequestData.Usuario;

            return dictDelivery;
        }
        private IDictionary<string, object> HeaderDocumentDelivery(dynamic orderData, AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            dynamic delivery = new ExpandoObject();
            var dictDelivery = (IDictionary<string, object>)delivery;

            // Campos estándar
            dictDelivery["CardCode"] = (string)orderData.CardCode;
            dictDelivery["DocDate"] = RequestData.FechaEntrega.ToString("yyyy-MM-dd");
            dictDelivery["DocDueDate"] = (string)orderData.DocDueDate;
            dictDelivery["NumAtCard"] = (string)orderData.NumAtCard;
            dictDelivery["Comments"] = (string)orderData.Comments + " Basado en Pedidos de cliente: " + (string)orderData.DocNum + Environment.NewLine + Environment.NewLine + " Documento Creado Por Interfáz FFISA " + DateTime.Now.ToString();
            dictDelivery["SalesPersonCode"] = (int?)orderData.SalesPersonCode;
            dictDelivery["ContactPersonCode"] = (int?)orderData.ContactPersonCode;
            dictDelivery["PaymentGroupCode"] = (int?)orderData.PaymentGroupCode;
            dictDelivery["DocCurrency"] = (string)orderData.DocCurrency;
            dictDelivery["DocRate"] = (double?)orderData.DocRate;

            // Copiar automáticamente todos los UDFs de cabecera
            foreach (JProperty prop in ((JObject)orderData).Properties())
            {
                if (prop.Name.StartsWith("U_"))
                {
                    object value = null;

                    if (prop.Value.Type == JTokenType.Integer)
                        value = (int)prop.Value;
                    else if (prop.Value.Type == JTokenType.Float)
                        value = (double)prop.Value;
                    else if (prop.Value.Type == JTokenType.Boolean)
                        value = (bool)prop.Value;
                    else
                        value = prop.Value?.ToString();

                    // ✅ Solo copiar si tiene un valor no vacío
                    if (value != null && !(value is string s && string.IsNullOrWhiteSpace(s)))
                    {
                        dictDelivery[prop.Name] = value;
                    }
                }
            }


            // Agregar manualmente usuario y comentarios
            dictDelivery["U_ComHanheld"] = RequestData.Comentarios;
            dictDelivery["U_UsuIniSes"] = RequestData.Usuario;
            return dictDelivery;
        }
        private async Task<IDictionary<string, object>> LinesDocumentDeliveryDIAPI(
        IDictionary<string, object> dictDelivery,
        JArray LotesData,
        dynamic orderData,
        AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            // Inicializar líneas del documento
            dictDelivery["DocumentLines"] = new List<object>();

            // -----------------------------
            // ✅ AGRUPAR LOTES POR ARTÍCULO/LÍNEA
            // -----------------------------
            var lotesAgrupados = LotesData
                .GroupBy(l => new
                {
                    ItemCode = (string)l["Articulo"],
                    LineNum = (int)l["Linea"]
                })
                .Select(g => new
                {
                    g.Key.ItemCode,
                    g.Key.LineNum,
                    Lotes = g.Select(x => new
                    {
                        WarehouseCode = (string)x["Almacen"],
                        BatchNumber = (string)x["Lote"],
                        Cantidad = (decimal)x["Cantidad"],
                        CantidadConversion = (decimal)x["CantidadConversion"]
                    }).ToList()
                });

            // -----------------------------
            // ✅ CREAR LÍNEA ÚNICA DE ENTREGA
            // -----------------------------
            foreach (var grupo in lotesAgrupados)
            {
                string itemCode = grupo.ItemCode;
                int lineNum = grupo.LineNum;

                // Línea base de la OV
                var sourceLine = ((JArray)orderData.DocumentLines)
                    .FirstOrDefault(x => (int)x["LineNum"] == 0);

                if (sourceLine == null)
                    continue;

                // Obtener unidad de inventario
                string UMI = string.Empty;
                var urlItem = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Items('{itemCode}')";
                var itemResponse = await LoginService._httpClient.GetAsync(urlItem);

                if (itemResponse.IsSuccessStatusCode)
                {
                    var itemJson = await itemResponse.Content.ReadAsStringAsync();
                    dynamic itemData = JsonConvert.DeserializeObject(itemJson);
                    UMI = itemData.InventoryUOM.ToString().ToUpper();
                }
                else
                {
                    var error = await itemResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Error obteniendo InvntryUom para {itemCode}: {error}");
                }

                // Unidad solicitada
                string UMS = sourceLine["U_UMsolicitada"] != null ? sourceLine["U_UMsolicitada"].ToString() : string.Empty;
                switch (UMS)
                {
                    case "01": UMS = "MTS"; break;
                    case "02": UMS = "KG"; break;
                    case "03": UMS = "PZS"; break;
                    case "04": UMS = "YDS"; break;
                }

                // ✅ Unir todos los lotes sin importar el almacén
                var lotesParaLinea = grupo.Lotes.ToList();
                decimal totalCantidadNativa = lotesParaLinea.Sum(l => l.Cantidad);
                decimal totalCantidadConversion = lotesParaLinea.Sum(l => l.CantidadConversion);

                // ✅ Crear lista de lotes (batches) con almacén incluido
                var batches = lotesParaLinea.Select(l => new
                {
                    l.BatchNumber,
                    Quantity = Math.Round(l.Cantidad, 2)
                }).ToList();

                // ✅ CAMBIO AQUÍ: Crear nueva línea como Dictionary en lugar de ExpandoObject
                var newLine = new Dictionary<string, object>();

                // Copiar UDFs de la línea base
                foreach (JProperty prop in ((JObject)sourceLine).Properties())
                {
                    if (prop.Name.StartsWith("U_"))
                    {
                        object value = null;
                        if (prop.Value.Type == JTokenType.Integer)
                            value = (int)prop.Value;
                        else if (prop.Value.Type == JTokenType.Float)
                            value = (double)prop.Value;
                        else if (prop.Value.Type == JTokenType.Boolean)
                            value = (bool)prop.Value;
                        else
                            value = prop.Value != null ? prop.Value.ToString() : null;

                        if (value != null && !(value is string s && string.IsNullOrWhiteSpace(s)))
                            newLine[prop.Name] = value;
                    }
                }

                // Campos adicionales
                newLine["U_norollos"] = LotesData.Count;
                decimal ConversionYardas = 1.09361m;

                decimal precioNativo = decimal.Parse(sourceLine["Price"].ToString());
                decimal totalOV = (decimal)sourceLine["LineTotal"];

                // -----------------------------
                // CALCULO DE LineTotal SEGÚN UNIDADES
                // -----------------------------
                if ((UMI == "PZA" && UMS == "PZS") || (UMI == "KG" && UMS == "KG") || (UMI == "MTS" && UMS == "MTS"))
                {
                    newLine["LineTotal"] = Math.Round(totalCantidadNativa * precioNativo, 6);
                }
                else if (UMI == "KG" && UMS == "MTS")
                {
                    decimal cantidadMetros = totalCantidadConversion;
                    decimal cantidadYardas = cantidadMetros * ConversionYardas;
                    decimal precioPorMetro = decimal.Parse(sourceLine["U_Precioxmetro"].ToString());
                    decimal LineTotal = Math.Round(cantidadMetros * precioPorMetro, 6);

                    newLine["U_Cantidadenmetros"] = cantidadMetros.ToString();
                    newLine["U_Cantidadenyardas"] = cantidadYardas.ToString();
                    newLine["LineTotal"] = LineTotal;
                }
                else if ((UMI == "KG" || UMI == "MTS") && UMS == "YDS")
                {
                    decimal yardasToMetros = totalCantidadConversion;
                    decimal cantidadYardas = totalCantidadConversion * ConversionYardas;
                    decimal precioPorYarda = decimal.Parse(sourceLine["U_Precioxyarda"].ToString());
                    decimal LineTotal = Math.Round(cantidadYardas * precioPorYarda, 6);

                    newLine["U_Cantidadenmetros"] = yardasToMetros.ToString();
                    newLine["U_Cantidadenyardas"] = cantidadYardas.ToString();
                    newLine["LineTotal"] = LineTotal;
                }

                // Campos obligatorios
                newLine["ItemCode"] = itemCode;
                newLine["UnitPrice"] = Convert.ToDecimal(sourceLine["UnitPrice"]); // Siempre enviar UnitPrice original

                if (totalOV == (decimal)newLine["LineTotal"])
                {
                    newLine["Quantity"] = totalCantidadNativa;

                    // ✅ Si la cantidad entregada es diferente a la de la OV
                    if (totalCantidadNativa != Convert.ToDecimal(sourceLine["Quantity"]))
                    {
                        // Calcular descuento (puede ser negativo = recargo)
                        decimal unitPrice = Convert.ToDecimal(sourceLine["UnitPrice"]);
                        decimal totalBruto = totalCantidadNativa * unitPrice;
                        decimal totalDeseado = totalOV;

                        // Fórmula: DiscountPercent = ((TotalBruto - TotalDeseado) / TotalBruto) × 100
                        decimal discountPercent = ((totalBruto - totalDeseado) / totalBruto) * 100;

                        // ✅ Redondear a 6 decimales para precisión
                        newLine["DiscountPercent"] = Math.Round(discountPercent, 6);
                    }
                    else
                    {
                        // Cantidad igual, usar el precio/descuento original
                        newLine["Quantity"] = Convert.ToDecimal(sourceLine["Quantity"]);

                        // Si la OV tiene descuento, copiarlo
                        if (sourceLine["DiscountPercent"] != null)
                            newLine["DiscountPercent"] = Convert.ToDecimal(sourceLine["DiscountPercent"]);
                        else
                            newLine["DiscountPercent"] = 0;
                    }
                }
                else
                {
                    newLine["Quantity"] = totalCantidadNativa;

                    // Si los totales no coinciden, usar descuento 0 o el original
                    if (sourceLine["DiscountPercent"] != null)
                        newLine["DiscountPercent"] = Convert.ToDecimal(sourceLine["DiscountPercent"]);
                    else
                        newLine["DiscountPercent"] = 0;
                }

                // ✅ Como los lotes tienen distintos almacenes, solo ponemos el primero de referencia
                newLine["WarehouseCode"] = lotesParaLinea.First().WarehouseCode;
                newLine["BatchNumbers"] = batches;
                newLine["BaseType"] = 17;
                newLine["BaseEntry"] = RequestData.OrdenVenta;
                newLine["BaseLine"] = (int)sourceLine["LineNum"];

                // ------------------------------------------------------
                // ✅ AJUSTE FINAL DE LOTES
                // ------------------------------------------------------
                //decimal sumaLotes = batches.Sum(b => b.Quantity);
                //decimal cantidadLinea = Convert.ToDecimal(sourceLine["Quantity"]);
                //decimal diferencia = Math.Round(sumaLotes - cantidadLinea, 2);
                //Si se va entregar mas de lo que pide la orden de venta pero lleva conversion MTS,YDZ y el total de la OV es igual al total de la entrega
                //se ajustara el lote para que entrege la misma cantidad nativa que la OV para que las conversiones salgan bien
                //if (diferencia > 0)
                //{
                //    var ultimoLote = batches.Last();
                //    var loteAjustado = new
                //    {
                //        ultimoLote.BatchNumber,
                //        Quantity = Math.Round(ultimoLote.Quantity - diferencia, 2)
                //    };

                //    batches[batches.Count - 1] = loteAjustado;
                //    newLine["BatchNumbers"] = batches;
                //}

                // ✅ Solo UNA línea por BaseLine
                ((List<object>)dictDelivery["DocumentLines"]).Add(newLine);
            }

            return dictDelivery;
        }
        private async Task<IDictionary<string, object>> LinesDocumentDelivery(
        IDictionary<string, object> dictDelivery,
        JArray LotesData,
        dynamic orderData,
        AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            // Inicializar líneas del documento
            dictDelivery["DocumentLines"] = new List<object>();

            // -----------------------------
            // ✅ AGRUPAR LOTES POR ARTÍCULO/LÍNEA
            // -----------------------------
            var lotesAgrupados = LotesData
                .GroupBy(l => new
                {
                    ItemCode = (string)l["Articulo"],
                    LineNum = (int)l["Linea"]
                })
                .Select(g => new
                {
                    g.Key.ItemCode,
                    g.Key.LineNum,
                    Lotes = g.Select(x => new
                    {
                        WarehouseCode = (string)x["Almacen"],
                        BatchNumber = (string)x["Lote"],
                        Cantidad = (decimal)x["Cantidad"],
                        CantidadConversion = (decimal)x["CantidadConversion"]
                    }).ToList()
                });

            // -----------------------------
            // ✅ CREAR LÍNEA ÚNICA DE ENTREGA
            // -----------------------------
            foreach (var grupo in lotesAgrupados)
            {
                string itemCode = grupo.ItemCode;
                int lineNum = grupo.LineNum;

                // Línea base de la OV
                var sourceLine = ((JArray)orderData.DocumentLines)
                    .FirstOrDefault(x => (int)x["LineNum"] == 0);

                if (sourceLine == null)
                    continue;

                // Obtener unidad de inventario
                string UMI = string.Empty;
                var urlItem = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Items('{itemCode}')";
                var itemResponse = await LoginService._httpClient.GetAsync(urlItem);

                if (itemResponse.IsSuccessStatusCode)
                {
                    var itemJson = await itemResponse.Content.ReadAsStringAsync();
                    dynamic itemData = JsonConvert.DeserializeObject(itemJson);
                    UMI = itemData.InventoryUOM.ToString().ToUpper();
                }
                else
                {
                    var error = await itemResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Error obteniendo InvntryUom para {itemCode}: {error}");
                }

                // Unidad solicitada
                string UMS = sourceLine["U_UMsolicitada"] != null ? sourceLine["U_UMsolicitada"].ToString() : string.Empty;
                switch (UMS)
                {
                    case "01": UMS = "MTS"; break;
                    case "02": UMS = "KG"; break;
                    case "03": UMS = "PZS"; break;
                    case "04": UMS = "YDS"; break;
                }

                // ✅ Unir todos los lotes sin importar el almacén
                var lotesParaLinea = grupo.Lotes.ToList();
                decimal totalCantidadNativa = lotesParaLinea.Sum(l => l.Cantidad);
                decimal totalCantidadConversion = lotesParaLinea.Sum(l => l.CantidadConversion);

                // ✅ Crear lista de lotes (batches) con almacén incluido
                var batches = lotesParaLinea.Select(l => new
                {
                    l.BatchNumber,
                    Quantity = Math.Round(l.Cantidad, 2)
                }).ToList();

                // Crear nueva línea
                var newLine = new ExpandoObject() as IDictionary<string, object>;

                // Copiar UDFs de la línea base
                foreach (JProperty prop in ((JObject)sourceLine).Properties())
                {
                    if (prop.Name.StartsWith("U_"))
                    {
                        object value = null;
                        if (prop.Value.Type == JTokenType.Integer)
                            value = (int)prop.Value;
                        else if (prop.Value.Type == JTokenType.Float)
                            value = (double)prop.Value;
                        else if (prop.Value.Type == JTokenType.Boolean)
                            value = (bool)prop.Value;
                        else
                            value = prop.Value != null ? prop.Value.ToString() : null;

                        if (value != null && !(value is string s && string.IsNullOrWhiteSpace(s)))
                            newLine[prop.Name] = value;
                    }
                }

                // Campos adicionales
                newLine["U_norollos"] = LotesData.Count;
                decimal ConversionYardas = 1.09361m;

                decimal precioNativo = decimal.Parse(sourceLine["Price"].ToString());
                decimal totalOV = (decimal)sourceLine["LineTotal"];

                // -----------------------------
                // CALCULO DE LineTotal SEGÚN UNIDADES
                // -----------------------------
                if ((UMI == "PZA" && UMS == "PZS") || (UMI == "KG" && UMS == "KG") || (UMI == "MTS" && UMS == "MTS"))
                {
                    newLine["LineTotal"] = Math.Round(totalCantidadNativa * precioNativo, 6);
                }
                else if (UMI == "KG" && UMS == "MTS")
                {
                    decimal cantidadMetros = totalCantidadConversion;
                    decimal cantidadYardas = cantidadMetros * ConversionYardas;
                    decimal precioPorMetro = decimal.Parse(sourceLine["U_Precioxmetro"].ToString());
                    decimal LineTotal = Math.Round(cantidadMetros * precioPorMetro, 6);

                    newLine["U_Cantidadenmetros"] = cantidadMetros.ToString();
                    newLine["U_Cantidadenyardas"] = cantidadYardas.ToString();
                    newLine["LineTotal"] = LineTotal;
                }
                else if ((UMI == "KG" || UMI == "MTS") && UMS == "YDS")
                {
                    decimal yardasToMetros = totalCantidadConversion;
                    decimal cantidadYardas = totalCantidadConversion * ConversionYardas;
                    decimal precioPorYarda = decimal.Parse(sourceLine["U_Precioxyarda"].ToString());
                    decimal LineTotal = Math.Round(cantidadYardas * precioPorYarda, 6);

                    newLine["U_Cantidadenmetros"] = yardasToMetros.ToString();
                    newLine["U_Cantidadenyardas"] = cantidadYardas.ToString();
                    newLine["LineTotal"] = LineTotal;
                }

                // Campos obligatorios
                newLine["ItemCode"] = itemCode;

                if (totalOV == (decimal)newLine["LineTotal"])
                {
                    //Si la cantidad nativa que se esta entregando en los lotes es igual a la de la orde de venta
                    if (totalCantidadNativa == Convert.ToDecimal(sourceLine["Quantity"]))
                        newLine["Quantity"] = Convert.ToDecimal(sourceLine["Quantity"]);
                    else
                    {
                        newLine["Quantity"] = totalCantidadNativa;
                        //Enviar porcentaje descuento
                        decimal unitPrice = Convert.ToDecimal(sourceLine["UnitPrice"]);
                        decimal quantity = totalCantidadNativa;
                        decimal totalOVLinea = totalOV;
                        newLine["DiscountPercent"] = GlobalCommands.CalcularDiscountPercentExacto(unitPrice, quantity, totalOVLinea);
                    }
                }

                else
                    newLine["Quantity"] = totalCantidadNativa;

                // ✅ Como los lotes tienen distintos almacenes, solo ponemos el primero de referencia
                newLine["WarehouseCode"] = lotesParaLinea.First().WarehouseCode;
                newLine["BatchNumbers"] = batches;
                newLine["BaseType"] = 17;
                newLine["BaseEntry"] = RequestData.OrdenVenta;
                newLine["BaseLine"] = (int)sourceLine["LineNum"];

                // ------------------------------------------------------
                // ✅ AJUSTE FINAL DE LOTES
                // ------------------------------------------------------
                //decimal sumaLotes = batches.Sum(b => b.Quantity);
                //decimal cantidadLinea = Convert.ToDecimal(sourceLine["Quantity"]);
                //decimal diferencia = Math.Round(sumaLotes - cantidadLinea, 2);
                //Si se va entregar mas de lo que pide la orden de venta pero lleva conversion MTS,YDZ y el total de la OV es igual al total de la entrega
                //se ajustara el lote para que entrege la misma cantidad nativa que la OV para que las conversiones salgan bien
                //if (diferencia > 0)
                //{
                //    var ultimoLote = batches.Last();
                //    var loteAjustado = new
                //    {
                //        ultimoLote.BatchNumber,
                //        Quantity = Math.Round(ultimoLote.Quantity - diferencia, 2)
                //    };

                //    batches[batches.Count - 1] = loteAjustado;
                //    newLine["BatchNumbers"] = batches;
                //}

                // ✅ Solo UNA línea por BaseLine
                ((List<object>)dictDelivery["DocumentLines"]).Add(newLine);
            }

            return dictDelivery;
        }
        private void UpdateLotesEntrega(
        JArray LotesData,
        dynamic orderData,
        AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            // Agrupar solo por ItemCode y LineNum
            var lotesAgrupados = LotesData
                .GroupBy(l => new
                {
                    ItemCode = (string)l["Articulo"],
                    LineNum = (int)l["Linea"]
                })
                .Select(g => new
                {
                    g.Key.ItemCode,
                    g.Key.LineNum,
                    // Lotes proyectados con WarehouseCode disponible
                    Lotes = g.Select(x => new
                    {
                        WarehouseCode = (string)x["Almacen"],
                        BatchNumber = (string)x["Lote"],
                        Cantidad = (decimal)x["Cantidad"]
                    }).ToList()
                });

            // Recorrer los grupos
            foreach (var grupo in lotesAgrupados)
            {
                string itemCode = grupo.ItemCode;

                foreach (var lote in grupo.Lotes)
                {
                    string batchNumber = lote.BatchNumber;
                    string warehouseCode = lote.WarehouseCode;

                    var loteUpdateParams = new Dictionary<string, string>
                {
                    { "BatchNumber", batchNumber },
                    { "ItemCode", itemCode },
                    { "Fecha", RequestData.FechaEntrega.ToString("dd-MM-yyyy HH:mm:ss") },
                    { "OrdenVenta", (string)orderData.DocNum },
                    { "Cliente", (string)orderData.CardCode },
                    { "CantidadReferencia", string.Empty }
                };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaLoteVentasHHEMOV, loteUpdateParams);
                }
            }
        }
        private void UpdateDocumentDelivery(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocNum)
        {
            var updateParams = new Dictionary<string, string>
                          {
                              { "OrdenVenta", RequestData.OrdenVenta.ToString() },
                              { "Folio", RequestData.FolioEM },
                              { "EntregaMercancia", DocNum },
                              { "Borrador", "N/A" }
                          };

            GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosVentasHHEMOV, updateParams);

        }
        private async Task NotifyWebQuanqityRollos(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, 4);
                    string url = ConfigurationManager.AppSettings["EndPointActualizacionRollos"];
                    var json = "{ \"pedido\": \"" + RequestData.OrdenVenta + "\", \"rollos\":" + RequestData.Rollos + "}";
                    var contentPedido = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(url, contentPedido);
                    string responseBody = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private async Task UpdateMontoLetras(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocEntry)
        {
            var deliveryUrlGet = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes({DocEntry})";
            var deliveryResponse = await LoginService._httpClient.GetAsync(deliveryUrlGet);

            if (deliveryResponse.IsSuccessStatusCode)
            {
                var deliveryJson = await deliveryResponse.Content.ReadAsStringAsync();
                dynamic deliveryData = JsonConvert.DeserializeObject(deliveryJson);

                string docCurrency = (string)deliveryData.DocCurrency;

                // DECIDIR QUÉ MONTO USAR: Si no es MXN, usar DocTotalFC
                decimal totalFinal = (docCurrency != "MXN" && docCurrency != "MXP")
                    ? (decimal)deliveryData.DocTotalFc  // Moneda extranjera
                    : (decimal)deliveryData.DocTotal;   // Moneda local (MXN)

                string codigoMoneda = ObtenerCodigoMoneda(docCurrency);

                string totalEnLetras = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "es").ToUpper();
                string totalEnLetrasEN = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "en").ToUpper();

                var montoParams = new Dictionary<string, string>
                {
                    { "Entrega", DocEntry },
                    { "MontoES", totalEnLetras },
                    { "MontoEN", totalEnLetrasEN },
                    { "TipoDocumento", "ENTREGA" }
                };

                string result = GlobalCommands.ExecuteProcedure(AD.GCActualizaMontoLetrasHHEMOV, montoParams);
            }
            else
            {
                var errorGet = await deliveryResponse.Content.ReadAsStringAsync();
                throw new Exception(errorGet);
            }
        }
        // Método auxiliar para mapear códigos de moneda de SAP
        private string ObtenerCodigoMoneda(string sapCurrencyCode)
        {
            // Diccionario para mapear códigos SAP a códigos estándar
            var mapMonedas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "MXN", "MXN" },    // Peso Mexicano
            { "USD", "USD" },    // Dólar Americano
            { "EUR", "EUR" },    // Euro
            { "GBP", "GBP" },    // Libra Esterlina
            { "CAD", "CAD" },    // Dólar Canadiense
            { "JPY", "JPY" },    // Yen Japonés
            { "CNY", "CNY" },    // Yuan Chino
            { "BRL", "BRL" },    // Real Brasileño
            // Agrega más monedas según necesites...
        };

            // Si encontramos el código, lo usamos. Si no, usamos el código original.
            if (mapMonedas.TryGetValue(sapCurrencyCode, out string codigoStandard))
            {
                return codigoStandard;
            }
            else
            {
                // Log de advertencia por si hay una moneda no contemplada
                Console.WriteLine($"Advertencia: Código de moneda '{sapCurrencyCode}' no está mapeado. Usando código original.");
                return sapCurrencyCode;
            }
        }
        // Métodos auxiliares específicos para borrador (ligeras modificaciones)
        private void UpdateLotesEntregaDraft(
        JArray LotesData,
        dynamic orderData,
        AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            // Agrupar solo por ItemCode y LineNum
            var lotesAgrupados = LotesData
                .GroupBy(l => new
                {
                    ItemCode = (string)l["Articulo"],
                    LineNum = (int)l["Linea"]
                })
                .Select(g => new
                {
                    g.Key.ItemCode,
                    g.Key.LineNum,
                    // Lotes proyectados con WarehouseCode disponible
                    Lotes = g.Select(x => new
                    {
                        WarehouseCode = (string)x["Almacen"],
                        BatchNumber = (string)x["Lote"],
                        CantidadReferencia = x["CantidadReferencia"] != null ? x["CantidadReferencia"].ToString() : string.Empty
                    }).ToList()
                });

            // Recorrer los grupos
            foreach (var grupo in lotesAgrupados)
            {
                string itemCode = grupo.ItemCode;

                foreach (var lote in grupo.Lotes)
                {
                    string batchNumber = lote.BatchNumber;
                    string warehouseCode = lote.WarehouseCode; // disponible si necesitas
                    string cantidadReferencia = lote.CantidadReferencia;

                    var loteUpdateParams = new Dictionary<string, string>
            {
                { "BatchNumber", batchNumber },
                { "ItemCode", itemCode },
                { "Fecha", RequestData.FechaEntrega.ToString("dd-MM-yyyy HH:mm:ss") },
                { "OrdenVenta", (string)orderData.DocNum },
                { "Cliente", (string)orderData.CardCode },
                { "CantidadReferencia", cantidadReferencia } // específico para borrador
            };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaLoteVentasHHEMOV, loteUpdateParams);
                }
            }
        }
        private void UpdateDocumentDraft(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocNum)
        {
            var updateParams = new Dictionary<string, string>
            {
                { "OrdenVenta", RequestData.OrdenVenta.ToString() },
                { "Folio", RequestData.FolioEM },
                { "EntregaMercancia", "N/A" }, // Específico para borrador
                { "Borrador", DocNum } // Específico para borrador
            };

            GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosVentasHHEMOV, updateParams);
        }
        private async Task UpdateMontoLetrasDraft(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocEntry)
        {
            // Obtener el borrador completo para tener DocTotal Y DocCurrency
            var draftUrlGet = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Drafts({DocEntry})";
            var draftResponse = await LoginService._httpClient.GetAsync(draftUrlGet);

            if (draftResponse.IsSuccessStatusCode)
            {
                var draftJson = await draftResponse.Content.ReadAsStringAsync();
                dynamic draftData = JsonConvert.DeserializeObject(draftJson);


                string docCurrency = (string)draftData.DocCurrency; // 👈 OBTENER LA MONEDA DEL BORRADOR
                // Por esto:
                decimal totalFinal = (docCurrency != "MXN" && docCurrency != "MXP")
                    ? (decimal)draftData.DocTotalFc
                    : (decimal)draftData.DocTotal;

                // Determinar el código de moneda para la conversión
                string codigoMoneda = ObtenerCodigoMoneda(docCurrency);

                // Convertir a letras (USANDO la moneda real del documento)
                string totalEnLetras = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "es").ToUpper(); // Español
                string totalEnLetrasEN = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "en").ToUpper(); // Inglés

                // -----------------------------
                // LLAMAR STORED PROCEDURE PARA ACTUALIZAR UDFs
                // -----------------------------
                var montoParams = new Dictionary<string, string>
                {
                    { "Entrega", DocEntry },
                    { "MontoES", totalEnLetras },
                    { "MontoEN", totalEnLetrasEN },
                    { "TipoDocumento", "BORRADOR" }
                };

                string result = GlobalCommands.ExecuteProcedure(AD.GCActualizaMontoLetrasHHEMOV, montoParams);

                // Opcional: Validar el resultado del stored procedure
                if (result.Contains("Error") || !string.IsNullOrEmpty(result))
                {
                    Console.WriteLine($"Resultado de actualización de monto: {result}");
                }
            }
            else
            {
                var errorGet = await draftResponse.Content.ReadAsStringAsync();
                throw new Exception($"Error al obtener borrador para monto en letras: {errorGet}");
            }
        }
        #endregion

        #region SBO VENTAS ENTREGAS ALTERNO
        //Notificacion a facturacion
        public bool NotificacionFacturacion(string Asunto, string Titulo, string AsuntoHtml, string Mensaje, string OrdenVenta, string Area)
        {

            try
            {

                //Correo de confirmación de correo enviado
                string htmlBody = $@"
                                    <!DOCTYPE html>
                                    <html lang=""es"">
                                      <head>
                                        <meta charset=""UTF-8"">
                                        <title>{Titulo}</title>
                                      </head>
                                      <body style=""margin:0; padding:0; background-color:#f4f4f4; font-family: Arial, sans-serif;"">
                                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" bgcolor=""#f4f4f4"">
                                          <tr>
                                            <td align=""center"">
                                              <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#ffffff;"">
                                                <!-- Encabezado -->
                                                <tr>
                                                  <td style=""background-color:#000000; padding:20px 0; text-align:center;"">
                                                    <img src=""cid:imgAct"" alt=""Encabezado"" width=""300"" height=""102"" style=""display:block; margin:auto;"">
                                                  </td>
                                                </tr>

                                                <!-- Título -->
                                                <tr>
                                                  <td style=""padding:30px 40px 10px 40px; color:#333333; font-size:20px; font-weight:bold; text-align:center;"">
                                                    {AsuntoHtml}
                                                  </td>
                                                </tr>

                                                <!-- Cuerpo -->
                                                <tr>
                                                  <td style=""padding:10px 40px 30px 40px; color:#555555; font-size:16px; line-height:1.6;"">
                                                    {Mensaje}
                                                  </td>
                                                </tr>
                                                <!-- Pie de página -->
                                                <tr>
                                                  <td style=""background-color:#eeeeee; padding:15px 40px; text-align:center; font-size:12px; color:#888888;"">
                                                    © 2025 Paradox ET S. de R.L. de C.V. Todos los derechos reservados.
                                                  </td>
                                                </tr>
                                              </table>
                                            </td>
                                          </tr>
                                        </table>
                                      </body>
                                    </html>
                                    ";

                // Email el mensaje de correo
                var Email = new MailMessage
                {
                    From = new MailAddress(ConfigurationManager.AppSettings["EmailFrom"], "Entregas"),
                    Subject = Asunto,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                // Crear el recurso HTML con imagen embebida
                AlternateView avHtml = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);

                // Ruta física del archivo PNG
                string rutaImagen = HttpContext.Current.Server.MapPath("~/assets/img/logoFisfiber.png");

                // Crear el recurso de la imagen
                LinkedResource img = new LinkedResource(rutaImagen, "image/png");
                img.ContentId = "imgAct";  // Este debe coincidir con el cid en el HTML: <img src="cid:imgAct">
                img.ContentType.Name = "FFISA";
                img.TransferEncoding = TransferEncoding.Base64;

                // Adjuntar la imagen embebida a la vista HTML
                avHtml.LinkedResources.Add(img);

                // Agregar la vista alternativa al correo
                Email.AlternateViews.Add(avHtml);


                //LA CONFIGURACION DEL SMTP SE PEUDE CAMBIAR EN EL WEB CONFIG
                var smtpMail = new SmtpClient
                {
                    Host = ConfigurationManager.AppSettings["EmailHost"],
                    Port = int.Parse(ConfigurationManager.AppSettings["EmailPort"]),
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                     ConfigurationManager.AppSettings["EmailFrom"],
                     ConfigurationManager.AppSettings["EmailPassword"])
                };

                // Obtener destinatarios
                JArray Emails = JArray.Parse((Area == "Facturación" ? EmailsFacturacion : EmailsVentas));
                foreach (JObject email in Emails)
                {
                    var direcciones = email["Email"].ToString().Split(',');
                    foreach (var direccion in direcciones)
                    {
                        if (!string.IsNullOrWhiteSpace(direccion))
                            Email.To.Add(direccion.Trim());
                    }
                }

                smtpMail.Send(Email);
                smtpMail.Dispose();
                Email.Dispose();
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        #endregion

    }
}