using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class LogicaVentasMovil
    {
        #region variables
        public AccesoDatosVentas AD = new AccesoDatosVentas();
        public LoginServiceLayer LoginService = new LoginServiceLayer();
        public GlobalCommands GlobalCommands = new GlobalCommands();
        private static readonly ILog log = LogManager.GetLogger("VentasMovil");
        private string EmailsFacturacion { get; }
        private string EmailsVentas { get; }
        #endregion

        #region constructor
        public LogicaVentasMovil() //se ejecuta al instanciar la clase
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
        // Método para crear BORRADOR de entrega desde orden (totalmente refactorizado con logs completos)
        public async Task<GlobalCommands.SapResponse> CreateDeliveryDraftAutomaticFromOrderAsync(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };

            try
            {
                log.Info("=======================================================================================");
                log.Info("==================CREAR BORRADOR DE ENTREGA================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando creación de borrador de entrega | Usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | OV DocEntry: {RequestData.OrdenVenta}  Con fecha de entrega {RequestData.FechaEntrega}");

                // -----------------------------
                // PASO 1: Obtener orden de venta (REUTILIZABLE)
                // -----------------------------
                log.Info($"[INFO] Obteniendo orden de venta desde SAP | OV DocEntry: {RequestData.OrdenVenta}");
                var urlOrder = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({RequestData.OrdenVenta})";
                var orderResponse = await LoginService._httpClient.GetAsync(urlOrder);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    var error = await orderResponse.Content.ReadAsStringAsync();
                    log.Error($"[ERROR] No se pudo obtener la orden de venta | OV DocEntry: {RequestData.OrdenVenta} | Error: {error}");
                    responseAbx.Message = $"Error al obtener la orden: {error}";
                    return responseAbx;
                }

                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                dynamic orderData;

                try
                {
                    orderData = JsonConvert.DeserializeObject(orderJson);
                    log.Info($"[OK] Orden de venta obtenida exitosamente | OV DocEntry: {RequestData.OrdenVenta} | OV DocNum: {(string)orderData.DocNum}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ERROR] Error al deserializar la orden de venta | OV DocEntry: {RequestData.OrdenVenta} | Error: {ex.Message}");
                    responseAbx.Message = $"Error al procesar la orden: {ex.Message}";
                    return responseAbx;
                }

                // -----------------------------
                // PASO 2: Preparar objeto de borrador (ENCABEZADO DOCUMENTO)
                // -----------------------------
                log.Info($"[INFO] Preparando encabezado del borrador de entrega | FolioEM: {RequestData.FolioEM}");
                dynamic deliveryDraft = new ExpandoObject();
                var dictDraft = (IDictionary<string, object>)deliveryDraft;

                // Agregar código específico para borrador
                dictDraft["DocObjectCode"] = "15"; // Código SAP para entrega (draft)

                // REUTILIZAR método de cabecera
                try
                {
                    var headerFields = HeaderDocumentDelivery(orderData, RequestData);
                    foreach (var field in headerFields)
                    {
                        dictDraft[field.Key] = field.Value;
                    }
                    log.Info($"[OK] Encabezado del borrador preparado exitosamente | FolioEM: {RequestData.FolioEM}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ERROR] Error al preparar encabezado del borrador | FolioEM: {RequestData.FolioEM} | Error: {ex.Message}");
                    responseAbx.Message = $"Error al preparar encabezado: {ex.Message}";
                    return responseAbx;
                }

                // -----------------------------
                // PASO 2a: Obtener lotes escaneados (REUTILIZABLE)
                // -----------------------------
                log.Info($"[INFO] Obteniendo lotes escaneados | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");
                Dictionary<string, string> keys = new Dictionary<string, string>
                {
                    { "Folio", RequestData.FolioEM },
                    { "Usuario", RequestData.Usuario }
                };
                string lotes = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosVentasHHEMOV, keys);

                if (lotes == "[]")
                {
                    log.Error($"[ERROR] No se encontró información de lotes | FolioEM: {RequestData.FolioEM}");
                    responseAbx.Message = $"No se encontró información de lotes asociada al folio {RequestData.FolioEM}";
                    return responseAbx;
                }
                else if (lotes.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al obtener información de lotes | FolioEM: {RequestData.FolioEM} | Error: {lotes}");
                    responseAbx.Message = $"No fue posible obtener información de lotes asociada al folio {RequestData.FolioEM}: {lotes}";
                    return responseAbx;
                }

                JArray LotesData = JArray.Parse(lotes);
                log.Info($"[OK] Lotes obtenidos exitosamente | FolioEM: {RequestData.FolioEM} | Cantidad de lotes: {LotesData.Count}");

                // -----------------------------
                // PASO intermedio: Para el caso de los borradores que si permite crear N borradores del mismo documento se debe validar
                // si anteriormente ya se genero un borrador y hubo perdida de conexion y no se notifico que el borrador ya habia sido creado
                // -----------------------------
                try
                {
                    // ✅ Validar si ya existe
                    log.Info($"[INFO] Validando si ya existe un borrador previo | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");
                    string BorradorentregaExistente = ValidarBorradorEntregaExistente(RequestData.FolioEM, RequestData.Usuario);

                    if (!string.IsNullOrEmpty(BorradorentregaExistente))
                    {
                        // ✅ La entrega ya existe
                        log.Info($"[OK] Borrador de entrega existente encontrado | DocNum: {BorradorentregaExistente} | FolioEM: {RequestData.FolioEM}");

                        responseAbx.IsError = false;
                        responseAbx.Message = $"Borrador de entrega creado exitosamente. Documento SAP: {BorradorentregaExistente}" +
                                             Environment.NewLine + Environment.NewLine +
                                             "● Cantidad de rollos comprendidos en el documento preeliminar de entrega: " + LotesData.Count +
                                             Environment.NewLine + Environment.NewLine;

                        responseAbx.OrdenVenta = (string)orderData.DocNum;

                        log.Info($"[INFO] Actualizando borrador existente | DocNum: {BorradorentregaExistente}");

                        // -----------------------------
                        // ACTUALIZAR DOCUMENTO
                        // -----------------------------
                        try
                        {
                            UpdateDocumentDraft(RequestData, BorradorentregaExistente);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[ERROR] Error al actualizar borrador | DocNum: {BorradorentregaExistente} | Error: {ex.Message}");
                        }

                        return responseAbx;
                    }

                    log.Info($"[INFO] No se encontró borrador previo, continuando con la creación | FolioEM: {RequestData.FolioEM}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ERROR] Error al validar si existe borrador | FolioEM: {RequestData.FolioEM} | Error: {ex.Message}");
                }

                // -----------------------------
                // PASO 2b: Agregar líneas del documento (REUTILIZABLE)
                // -----------------------------
                log.Info($"[INFO] Agregando líneas del borrador | FolioEM: {RequestData.FolioEM} | Cantidad de líneas: {LotesData.Count}");
                try
                {
                    dictDraft = await LinesDocumentDelivery(dictDraft, LotesData, orderData, RequestData);
                    log.Info($"[OK] Líneas del borrador agregadas exitosamente | FolioEM: {RequestData.FolioEM}");
                }
                catch (Exception E)
                {
                    log.Error($"[ERROR] Error al agregar líneas del borrador | FolioEM: {RequestData.FolioEM} | Error: {E.Message}");
                    responseAbx.Message = E.Message;
                    return responseAbx;
                }

                // -----------------------------
                // PASO 3: Verificar diferencias (ESPECÍFICO PARA BORRADOR)
                // -----------------------------
                log.Info($"[INFO] Verificando diferencias en lotes | FolioEM: {RequestData.FolioEM}");
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
                    log.Warn($"[WARN] Se encontraron {DiferenciaLotes.Count} diferencias en lotes | FolioEM: {RequestData.FolioEM}");
                    StringBuilder ConsideracionesLote = new StringBuilder();
                    foreach (var diferencia in DiferenciaLotes)
                    {
                        log.Warn($"[WARN] Diferencia en lote: {diferencia.Key} | Diferencia: {diferencia.Value} unidades");
                        ConsideracionesLote.AppendLine($"⚠️ Existe una diferencia de cantidad en el lote: {diferencia.Key} de {diferencia.Value} unidades respecto a la cantidad de referencia.");
                    }

                    log.Info($"[INFO] Enviando notificación de facturación");
                    NotificacionFacturacion(
                        $"Notificación de diferencias de cantidad en lote para la orden de venta {RequestData.OrdenVenta}",
                        $"Notificación de consideraciones para la orden de venta {RequestData.OrdenVenta} sobre la diferencia de cantidad en el lote",
                        $"🗒️ Hola, se deben tomar en cuenta las siguientes consideraciones sobre la diferencia de cantidad en el lote:",
                        ConsideracionesLote.ToString(),
                        RequestData.OrdenVenta.ToString(),
                        "Facturación"
                    );

                }
                else
                {
                    log.Info($"[OK] No se encontraron diferencias en lotes | FolioEM: {RequestData.FolioEM}");
                }

                // -----------------------------
                // PASO 4: Enviar borrador a SAP
                // -----------------------------
                log.Info($"[INFO] Enviando borrador de entrega a SAP Service Layer | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum}");
                var deliveryUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Drafts";
                var jsonBody = JsonConvert.SerializeObject(dictDraft);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(deliveryUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    log.Info($"[OK] Respuesta exitosa de SAP | FolioEM: {RequestData.FolioEM} | StatusCode: {postResponse.StatusCode}");
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    responseAbx.IsError = false;
                    string DocNum = string.Empty;
                    string DocEntry = string.Empty;

                    // ✅ AGREGAR ESTA VALIDACIÓN
                    // SAP puede retornar warnings/errores incluso con status 201
                    if (created.DocNum != null)
                    {
                        // Verificar si hay mensajes de error o warning
                        bool hasErrors = false;
                        string warningMessage = "";

                        if (created.error != null)
                        {
                            hasErrors = true;
                            warningMessage = created.error.message?.value ?? created.error.ToString();
                        }

                        if (hasErrors)
                        {
                            log.Error($"[ERROR] Borrador creado con errores en SAP | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum} | Error: {warningMessage}");
                            responseAbx.IsError = true;
                            responseAbx.Message = $"Borrador de Entrega creada con errores: {warningMessage}";
                            responseAbx.OrdenVenta = (string)orderData.DocNum;

                            // ⚠️ CRÍTICO: NO hacer las actualizaciones de lotes/documento
                            // porque el documento puede estar en estado inconsistente
                            return responseAbx;
                        }
                        else
                        {
                            responseAbx.IsError = false;
                            DocNum = created.DocNum;
                            DocEntry = created.DocEntry;
                            log.Info($"[OK] Borrador de entrega creado exitosamente en SAP | DocNum: {DocNum} | DocEntry: {DocEntry} | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum}");
                            responseAbx.Message = $"Borrador de entrega creado exitosamente. Documento SAP: {DocNum}" + Environment.NewLine + Environment.NewLine + "● Cantidad de rollos comprendidos en el documento preeliminar de entrega: " + LotesData.Count;
                        }

                        // -----------------------------
                        // ACTUALIZAR LOTES (REUTILIZABLE con modificación)
                        // -----------------------------
                        log.Info($"[INFO] Actualizando lotes del borrador | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM}");
                        UpdateLotesEntregaDraft(LotesData, orderData, RequestData);

                        // -----------------------------
                        // ACTUALIZAR DOCUMENTO (REUTILIZABLE con modificación)
                        // -----------------------------
                        log.Info($"[INFO] Actualizando documento borrador | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM}");
                        UpdateDocumentDraft(RequestData, DocNum);

                        // -----------------------------
                        // NOTIFICAR A WEB (REUTILIZABLE)
                        // -----------------------------
                        log.Info($"[INFO] Notificando a WEB cantidad de rollos | OV DocEntry: {RequestData.OrdenVenta}");
                        await NotifyWebQuanqityRollos(RequestData);
                        
                        // -----------------------------
                        // ACTUALIZAR MONTO EN LETRAS USANDO STORED PROCEDURE
                        // -----------------------------
                        log.Info($"[INFO] Actualizando monto en letras del borrador | DocEntry: {DocEntry}");
                        await UpdateMontoLetrasDraft(RequestData, DocEntry);
                    }
                    else
                    {
                        // No se obtuvo DocNum = algo falló
                        log.Error($"[ERROR] No se pudo crear el borrador, DocNum nulo | FolioEM: {RequestData.FolioEM} | Respuesta SAP: {postResult}");
                        responseAbx.IsError = true;
                        responseAbx.Message = "No se pudo crear el borrador de entrega: " + postResult;
                    }
                }
                else
                {
                    log.Error($"[ERROR] No se pudo crear el borrador, Respuesta de error de SAP | FolioEM: {RequestData.FolioEM} | StatusCode: {postResponse.StatusCode}");
                    //VALIDACION DOBLE DE CREACION DE BORRADOR EN DB Y SAP
                    try
                    {
                        // ✅ Validar si ya existe
                        log.Info($"[INFO] Validando si ya existe un borrador debido al error de SAP | FolioEM: {RequestData.FolioEM}");
                        string BorradorentregaExistente = ValidarBorradorEntregaExistente(RequestData.FolioEM, RequestData.Usuario);

                        if (!string.IsNullOrEmpty(BorradorentregaExistente))
                        {
                            // ✅ La entrega ya existe
                            log.Info($"[OK] Borrador existente encontrado después de error SAP | DocNum: {BorradorentregaExistente} | FolioEM: {RequestData.FolioEM}");

                            responseAbx.IsError = false;
                            responseAbx.Message = $"Borrador de entrega creado exitosamente. Documento SAP: {BorradorentregaExistente}" +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "● Cantidad de rollos comprendidos en el documento preeliminar de entrega: " + LotesData.Count +
                                                 Environment.NewLine + Environment.NewLine;

                            responseAbx.OrdenVenta = (string)orderData.DocNum;

                            log.Info($"[INFO] Actualizando borrador existente | DocNum: {BorradorentregaExistente}");

                            // -----------------------------
                            // ACTUALIZAR DOCUMENTO
                            // -----------------------------
                            try
                            {
                                UpdateDocumentDraft(RequestData, BorradorentregaExistente);
                                log.Info($"[OK] Borrador actualizado exitosamente | DocNum: {BorradorentregaExistente}");
                            }
                            catch (Exception ex)
                            {
                                log.Error($"[ERROR] Error al actualizar borrador | DocNum: {BorradorentregaExistente} | Error: {ex.Message}");
                            }

                            return responseAbx;
                        }

                        // ❌ Error real de SAP
                        log.Info($"[INFO] No se encontró borrador previo, procesando error de SAP | FolioEM: {RequestData.FolioEM}");

                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message?.value ?? errorObj.error.message?.ToString();

                        log.Error($"[ERROR] Error SAP confirmado | FolioEM: {RequestData.FolioEM} | ErrorCode: {errorCode} | Error: {errorMessage}");

                        responseAbx.IsError = true;
                        responseAbx.Message = $"{errorCode} {errorMessage}";
                        responseAbx.OrdenVenta = (string)orderData.DocNum;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error en manejo de respuesta de error SAP | FolioEM: {RequestData.FolioEM} | Error: {ex.Message} | Respuesta SAP: {postResult}");
                        responseAbx.IsError = true;
                        responseAbx.Message = $"{postResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en CreateDeliveryDraftAutomaticFromOrderAsync | Usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | OV DocEntry: {RequestData.OrdenVenta} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            log.Info($"[INFO] Finalizando CreateDeliveryDraftAutomaticFromOrderAsync | FolioEM: {RequestData.FolioEM} | IsError: {responseAbx.IsError}");
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
                log.Info("=======================================================================================");
                log.Info("==================CREAR ENTREGA EN FIRME================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando creación de entrega | Usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | OV DocEntry: {RequestData.OrdenVenta} Con fecha de entrega {RequestData.FechaEntrega}");

                // -----------------------------
                // PASO 1: Obtener orden de venta
                // -----------------------------
                log.Info($"[INFO] Obteniendo orden de venta desde SAP | OV DocEntry: {RequestData.OrdenVenta}");

                var urlOrder = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({RequestData.OrdenVenta})";
                var orderResponse = await LoginService._httpClient.GetAsync(urlOrder);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    var error = await orderResponse.Content.ReadAsStringAsync();
                    log.Error($"[ERROR] No se pudo obtener la orden de venta | OV DocEntry: {RequestData.OrdenVenta} | Error: {error}");
                    responseAbx.Message = $"Error al obtener la orden: {error}";
                    return responseAbx;
                }

                var orderJson = await orderResponse.Content.ReadAsStringAsync();
                dynamic orderData = JsonConvert.DeserializeObject(orderJson);

                log.Info($"[OK] Orden de venta obtenida exitosamente | OV DocEntry: {RequestData.OrdenVenta} | OV DocNum: {(string)orderData.DocNum}");

                // -----------------------------
                // PASO 2: Preparar objeto de entrega (ENCABEZADO DOCUMENTO)
                // -----------------------------
                log.Info($"[INFO] Preparando encabezado del documento de entrega | FolioEM: {RequestData.FolioEM}");

                dynamic delivery = new ExpandoObject();
                var dictDelivery = (IDictionary<string, object>)delivery;
                dictDelivery = HeaderDocumentDelivery(orderData, RequestData);

                // -----------------------------
                // PASO 2a: Obtener lotes escaneados (LINEAS DOCUMENTO)
                // -----------------------------
                log.Info($"[INFO] Obteniendo lotes escaneados | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");

                Dictionary<string, string> keys = new Dictionary<string, string>
                {
                    { "Folio", RequestData.FolioEM },
                    { "Usuario", RequestData.Usuario }
                };
                string lotes = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosVentasHHEMOV, keys);

                if (lotes == "[]")
                {
                    log.Error($"[ERROR] No se encontró información de lotes | FolioEM: {RequestData.FolioEM}");
                    responseAbx.Message = $"No se encontró información de lotes asociada al folio {RequestData.FolioEM}";
                    return responseAbx;
                }
                else if (lotes.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al obtener información de lotes | FolioEM: {RequestData.FolioEM} | Error: {lotes}");
                    responseAbx.Message = $"No fue posible obtener información de lotes asociada al folio {RequestData.FolioEM}: {lotes}";
                    return responseAbx;
                }

                JArray LotesData = JArray.Parse(lotes);
                log.Info($"[OK] Lotes obtenidos exitosamente | FolioEM: {RequestData.FolioEM} | Cantidad de lotes: {LotesData.Count}");

                try
                {
                    // ✅ Validar si ya existe antes de volver a enviar
                    log.Info($"[INFO] Validando si ya existe una entrega previa | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");

                    string entregaExistenteF = ValidarEntregaExistente(RequestData.FolioEM, RequestData.Usuario);

                    if (!string.IsNullOrEmpty(entregaExistenteF))
                    {
                        // ✅ La entrega ya existe
                        log.Info($"[OK] Entrega existente encontrada | DocNum: {entregaExistenteF} | FolioEM: {RequestData.FolioEM}");

                        responseAbx.IsError = false;
                        responseAbx.Message = $"Entrega creada exitosamente. Documento SAP: {entregaExistenteF}" +
                                             Environment.NewLine + Environment.NewLine +
                                             "● Cantidad de rollos comprendidos en la entrega: " + LotesData.Count +
                                             Environment.NewLine + Environment.NewLine;

                        responseAbx.OrdenVenta = (string)orderData.DocNum;

                        log.Info($"[INFO] Actualizando documento de entrega existente | DocNum: {entregaExistenteF} | FolioEM: {RequestData.FolioEM}");

                        // -----------------------------
                        // ACTUALIZAR DOCUMENTO
                        // -----------------------------
                        UpdateDocumentDelivery(RequestData, entregaExistenteF);

                        log.Info($"[OK] Documento actualizado exitosamente | DocNum: {entregaExistenteF}");

                        return responseAbx;
                    }

                    //❌ Error real de SAP
                    log.Info($"[INFO] No se encontró entrega previa, continuando con la creación | FolioEM: {RequestData.FolioEM}");
                }
                catch (Exception ex)
                {
                    log.Error($"[ERROR] Error al validar si existe entrega | FolioEM: {RequestData.FolioEM} | Error: {ex.Message}");
                }

                //GUARDAR DATOS REELEVANTES DE LA ORDEN PARA VALIDACIONES FINALES
                responseAbx.OrdenVenta = (string)orderData.DocNum;
                responseAbx.RollosComprendidos = LotesData.Count.ToString();

                // Agregar líneas del documento
                log.Info($"[INFO] Agregando líneas del documento | FolioEM: {RequestData.FolioEM} | Cantidad de líneas: {LotesData.Count}");

                try
                {
                    dictDelivery = await LinesDocumentDelivery(dictDelivery, LotesData, orderData, RequestData);
                    log.Info($"[OK] Líneas del documento agregadas exitosamente | FolioEM: {RequestData.FolioEM}");
                }
                catch (Exception E)
                {
                    log.Error($"[ERROR] Error al agregar líneas del documento | FolioEM: {RequestData.FolioEM} | Error: {E.Message}");
                    responseAbx.Message = E.Message;
                    return responseAbx;
                }

                // -----------------------------
                // PASO 3: Enviar entrega a SAP
                // -----------------------------
                log.Info($"[INFO] Enviando entrega a SAP Service Layer | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum}");

                var deliveryUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes";
                var jsonBody = JsonConvert.SerializeObject(dictDelivery);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(deliveryUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    log.Info($"[OK] Respuesta exitosa de SAP | FolioEM: {RequestData.FolioEM} | StatusCode: {postResponse.StatusCode}");

                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    string DocNum = string.Empty;
                    string DocEntryEntrega = string.Empty;
                    // ✅ AGREGAR ESTA VALIDACIÓN
                    // SAP puede retornar warnings/errores incluso con status 201
                    if (created.DocNum != null)
                    {
                        // Verificar si hay mensajes de error o warning
                        bool hasErrors = false;
                        string warningMessage = "";

                        if (created.error != null)
                        {
                            hasErrors = true;
                            warningMessage = created.error.message?.value ?? created.error.ToString();
                        }

                        if (hasErrors)
                        {
                            log.Error($"[ERROR] Entrega creada con errores en SAP | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum} | Error: {warningMessage}");

                            responseAbx.IsError = true;
                            responseAbx.Message = $"Entrega creada con errores: {warningMessage}";
                            responseAbx.OrdenVenta = (string)orderData.DocNum;

                            // ⚠️ CRÍTICO: NO hacer las actualizaciones de lotes/documento
                            // porque el documento puede estar en estado inconsistente
                            return responseAbx;
                        }
                        else
                        {
                            DocNum = created.DocNum;
                            DocEntryEntrega = created.DocEntry;

                            log.Info($"[OK] Entrega creada exitosamente en SAP | DocNum: {DocNum} | DocEntry: {DocEntryEntrega} | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum}");

                            //CERRAR ORDEN DE VENTA SI SE REQUIERE
                            // Línea base de la OV
                            var sourceLine = ((JArray)orderData["DocumentLines"]).First;

                            // Unidad solicitada
                            string UMS = sourceLine["U_UMsolicitada"] != null ? sourceLine["U_UMsolicitada"].ToString() : string.Empty;
                            switch (UMS)
                            {
                                case "01": UMS = "MTS"; break;
                                case "02": UMS = "KG"; break;
                                case "03": UMS = "PZS"; break;
                                case "04": UMS = "YDS"; break;
                            }

                            log.Info($"[INFO] Validando si la orden de venta debe cerrarse | OV DocEntry: {RequestData.OrdenVenta}");

                            Dictionary<string, string> parametros = new Dictionary<string, string>
                            {
                                { "DocEntry", RequestData.OrdenVenta.ToString() }
                            };

                            string CompleteOV = GlobalCommands.ExecuteProcedure(AD.GCValidarOrdenVentaCompletaPorRollosHH, parametros);

                            if (string.IsNullOrEmpty(CompleteOV) || CompleteOV == "[]" || CompleteOV.Contains("Error"))
                            {
                                log.Warn($"[WARN] No se pudo validar cierre de OV | OV: {RequestData.OrdenVenta}");

                                responseAbx.IsError = false;
                                responseAbx.Message =
                                    $"Entrega creada exitosamente. Documento SAP: {DocNum}" +
                                    Environment.NewLine + Environment.NewLine +
                                    "● Cantidad de rollos comprendidos en la entrega: " + LotesData.Count +
                                    Environment.NewLine + Environment.NewLine +
                                    "⚠ No fue posible validar si la orden de venta debe cerrarse.";

                                return responseAbx;
                            }

                            JArray OVComplete = JArray.Parse(CompleteOV);

                            if (OVComplete.Count == 0)
                            {
                                log.Warn($"[WARN] Consulta BD sin resultados | OV: {RequestData.OrdenVenta}");

                                responseAbx.IsError = false;
                                responseAbx.Message =
                                    $"Entrega creada exitosamente. Documento SAP: {DocNum}" +
                                    Environment.NewLine + Environment.NewLine +
                                    "● Cantidad de rollos comprendidos en la entrega: " + LotesData.Count +
                                    Environment.NewLine + Environment.NewLine +
                                    "⚠ No fue posible validar información de la orden.";

                                return responseAbx;
                            }

                            string EstaCompleta = OVComplete[0]["EstaCompleta"]?.ToString();
                            string DebeCerrarse = OVComplete[0]["DebeCerrarse"]?.ToString();

                            log.Info($"[INFO] Validación de cierre | OV DocEntry: {RequestData.OrdenVenta} | EstaCompleta: {EstaCompleta} | DebeCerrarse: {DebeCerrarse}");

                            //Solo las ordenes que son con conversion
                            if ((DebeCerrarse == "1" || DebeCerrarse == "True"))
                            {
                                log.Info($"[INFO] Cerrando orden de venta | OV DocEntry: {RequestData.OrdenVenta}");
                                await CerrarOrdenVentaSimpleAsync(RequestData.OrdenVenta.ToString());
                                log.Info($"[OK] Orden de venta cerrada exitosamente | OV DocEntry: {RequestData.OrdenVenta}");
                            }
                            else
                            {
                                log.Info($"[INFO] La orden de venta NO requiere cerrarse | OV DocEntry: {RequestData.OrdenVenta}");
                            }

                            responseAbx.IsError = false;
                            responseAbx.Message = $"Entrega creada exitosamente. Documento SAP: {DocNum}" + Environment.NewLine + Environment.NewLine + "● Cantidad de rollos comprendidos en la entrega: " + LotesData.Count;
                        }

                        // -----------------------------
                        // ACTUALIZAR LOTES
                        // -----------------------------
                        log.Info($"[INFO] Actualizando lotes de la entrega | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM}");
                        UpdateLotesEntrega(LotesData, orderData, RequestData);

                        // -----------------------------
                        // ACTUALIZAR DOCUMENTO
                        // -----------------------------
                        log.Info($"[INFO] Actualizando documento de entrega | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM}");
                        UpdateDocumentDelivery(RequestData, DocNum);

                        // -----------------------------
                        // NOTIFICACIONES
                        // -----------------------------
                        log.Info($"[INFO] Enviando notificación de facturación | DocNum: {DocNum} | OV: {(string)orderData.DocNum}");
                        bool notificacion = NotificacionFacturacion(
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
                        log.Info($"[INFO] Notificando a WEB cantidad de rollos | OV DocEntry: {RequestData.OrdenVenta}");
                        await NotifyWebQuanqityRollos(RequestData);

                        log.Info($"[INFO] Notificando a WEB creación de entrega | OV DocEntry: {RequestData.OrdenVenta} | DocEntry Entrega: {DocEntryEntrega}");
                        await NotifyWebEntrega(RequestData.OrdenVenta.ToString(), DocEntryEntrega);

                        // -----------------------------
                        // ACTUALIZAR MONTO EN LETRAS USANDO STORED PROCEDURE
                        // -----------------------------
                        log.Info($"[INFO] Actualizando monto en letras | DocEntry Entrega: {DocEntryEntrega}");
                        await UpdateMontoLetras(RequestData, DocEntryEntrega);
                    }
                    else
                    {
                        // No se obtuvo DocNum = algo falló
                        log.Error($"[ERROR] No se pudo crear la entrega, DocNum nulo | FolioEM: {RequestData.FolioEM} | Respuesta SAP: {postResult}");
                        responseAbx.IsError = true;
                        responseAbx.Message = "No se pudo crear la entrega: " + postResult;
                    }
                }
                else
                {
                    log.Error($"[ERROR] No se pudo crear la entrega, Respuesta de error de SAP | FolioEM: {RequestData.FolioEM} | StatusCode: {postResponse.StatusCode}");

                    try
                    {
                        // ✅ Validar si ya existe
                        log.Info($"[INFO] Validando si ya existe una entrega debido al error de SAP | FolioEM: {RequestData.FolioEM}");

                        string entregaExistente = ValidarEntregaExistente(RequestData.FolioEM, RequestData.Usuario);

                        if (!string.IsNullOrEmpty(entregaExistente))
                        {
                            // ✅ La entrega ya existe
                            log.Info($"[OK] Entrega existente encontrada después de error SAP | DocNum: {entregaExistente} | FolioEM: {RequestData.FolioEM}");

                            responseAbx.IsError = false;
                            responseAbx.Message = $"Entrega creada exitosamente. Documento SAP: {entregaExistente}" +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "● Cantidad de rollos comprendidos en la entrega: " + LotesData.Count +
                                                 Environment.NewLine + Environment.NewLine;

                            responseAbx.OrdenVenta = (string)orderData.DocNum;

                            log.Info($"[INFO] Actualizando documento de entrega existente | DocNum: {entregaExistente}");

                            // -----------------------------
                            // ACTUALIZAR DOCUMENTO
                            // -----------------------------
                            UpdateDocumentDelivery(RequestData, entregaExistente);

                            log.Info($"[OK] Documento actualizado exitosamente | DocNum: {entregaExistente}");

                            return responseAbx;
                        }

                        //❌ Error real de SAP
                        log.Info($"[INFO] No se encontró entrega previa, procesando error de SAP | FolioEM: {RequestData.FolioEM}");

                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message?.value ?? errorObj.error.message?.ToString();

                        log.Error($"[ERROR] Error SAP confirmado | FolioEM: {RequestData.FolioEM} | ErrorCode: {errorCode} | Error: {errorMessage}");

                        responseAbx.IsError = true;
                        responseAbx.Message = $"{errorCode} {errorMessage}";
                        responseAbx.OrdenVenta = (string)orderData.DocNum;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error en manejo de respuesta de error SAP,No se pudo crear la entrega | FolioEM: {RequestData.FolioEM} | Error: {ex.Message} | Respuesta SAP: {postResult}");
                        responseAbx.IsError = true;
                        responseAbx.Message = $"{postResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en CreateDeliveryFromOrderAutomaticAsync,No se pudo crear la entrega | Usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | OV DocEntry: {RequestData.OrdenVenta} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            return responseAbx;
        }

        #endregion

        #region SBO VENTAS ENTREGAS ALTERNO
        /// <summary>
        /// Cierra una Orden de Venta usando el servicio Close (más simple)
        /// </summary>
        public async Task<GlobalCommands.SapResponse> CerrarOrdenVentaSimpleAsync(string docEntry)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };

            try
            {
                log.Info($"Cerrando orden de venta - DocEntry: {docEntry}");

                // ============================================================
                // Usar el servicio Close directamente
                // ============================================================
                var urlClose = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Orders({docEntry})/Close";

                // El servicio Close no requiere body
                var closeResponse = await LoginService._httpClient.PostAsync(urlClose, null);

                if (closeResponse.IsSuccessStatusCode)
                {
                    log.Info($"✅ Orden cerrada exitosamente - DocEntry: {docEntry}");

                    responseAbx.IsError = false;
                    responseAbx.Message = $"Orden de venta cerrada exitosamente";

                    return responseAbx;
                }
                else
                {
                    var errorContent = await closeResponse.Content.ReadAsStringAsync();
                    log.Error($"Error al cerrar orden: {closeResponse.StatusCode} - {errorContent}");

                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(errorContent);
                        responseAbx.Message = $"{errorObj.error.code} - {errorObj.error.message.value}";
                    }
                    catch
                    {
                        responseAbx.Message = errorContent;
                    }

                    return responseAbx;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Excepción: {ex.Message}");
                responseAbx.Message = $"Excepción: {ex.Message}";
                return responseAbx;
            }
        }
        public async Task<GlobalCommands.SapResponse> CancelarEntregaMercanciaAsync(string docEntry)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };
            try
            {
                log.Info($"Cancelando entrega de mercancía - DocEntry: {docEntry}");

                // ============================================================
                // Usar el servicio Cancel directamente
                // ============================================================
                var urlCancel = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes({docEntry})/Cancel";

                // El servicio Cancel no requiere body
                var cancelResponse = await LoginService._httpClient.PostAsync(urlCancel, null);

                if (cancelResponse.IsSuccessStatusCode)
                {
                    log.Info($"✅ Entrega cancelada exitosamente - DocEntry: {docEntry}");
                    responseAbx.IsError = false;
                    responseAbx.Message = $"Entrega de mercancía cancelada exitosamente";
                    return responseAbx;
                }
                else
                {
                    var errorContent = await cancelResponse.Content.ReadAsStringAsync();
                    log.Error($"Error al cancelar entrega: {cancelResponse.StatusCode} - {errorContent}");

                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(errorContent);
                        responseAbx.Message = $"{errorObj.error.code} - {errorObj.error.message.value}";
                    }
                    catch
                    {
                        responseAbx.Message = errorContent;
                    }
                    return responseAbx;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Excepción al cancelar entrega: {ex.Message}");
                responseAbx.Message = $"Excepción: {ex.Message}";
                return responseAbx;
            }
        }
        /// <summary>
        /// Valida si una entrega ya existe en la base de datos (sin consultar Service Layer)
        /// Más confiable y rápido que validar en Service Layer
        /// </summary>
        public string ValidarEntregaExistente(string folio, string usuario)
        {
            try
            {
                log.Info($"Validando entrega existente para Folio: {folio}, Usuario: {usuario}");

                Dictionary<string, string> parametros = new Dictionary<string, string>
                {
                    { "Folio", folio },
                    { "Usuario", usuario }
                };

                string resultadoBD = GlobalCommands.ExecuteProcedure(AD.GCValidarEntregaExistenteHHEMOV, parametros);

                if (string.IsNullOrEmpty(resultadoBD) || resultadoBD == "[]" || resultadoBD.Contains("Error"))
                {
                    log.Info("No se encontró entrega en BD");
                    return null;
                }

                JArray entregas = JArray.Parse(resultadoBD);
                if (entregas.Count == 0)
                {
                    log.Info("Consulta BD retornó 0 registros");
                    return null;
                }

                // ============================================================
                // Analizar resultado del stored procedure
                // ============================================================
                var entrega = entregas[0];

                string entregaDocNum = entrega["EntregaMercancia"]?.ToString();
                string entregaDocEntry = entrega["EntregaDocEntry"]?.ToString();
                string cancelada = entrega["Cancelada"]?.ToString();
                string estadoSAP = entrega["EstadoSAP"]?.ToString();

                log.Info($"Entrega encontrada en BD:");
                log.Info($"   - DocNum: {entregaDocNum}");
                log.Info($"   - DocEntry: {entregaDocEntry}");
                log.Info($"   - Cancelada: {cancelada}");
                log.Info($"   - Estado SAP: {estadoSAP}");

                // Validar que tenga DocNum
                if (string.IsNullOrEmpty(entregaDocNum))
                {
                    log.Warn("EntregaMercancia es null o vacío");
                    return null;
                }

                // Validar que exista en ODLN (DocEntry no es null)
                if (string.IsNullOrEmpty(entregaDocEntry))
                {
                    log.Warn($"⚠️ Inconsistencia: Entrega {entregaDocNum} existe en tabla PDX pero NO en ODLN");
                    log.Warn($"   Posiblemente fue eliminada manualmente de SAP");
                    return null;
                }

                // Validar que no esté cancelada
                if (cancelada == "Y")
                {
                    log.Warn($"⚠️ La entrega {entregaDocNum} está CANCELADA en SAP");
                    return null;
                }

                // ✅ Todo OK - La entrega existe y es válida
                log.Info($"✅ Entrega {entregaDocNum} validada exitosamente (DocEntry: {entregaDocEntry}, Estado: {estadoSAP})");

                return entregaDocNum;
            }
            catch (Exception ex)
            {
                log.Error($"Error en ValidarEntregaExistente: {ex.Message}");
                log.Error($"StackTrace: {ex.StackTrace}");
                return null;
            }
        }
        /// <summary>
        /// Valida si un borrador de entrega ya existe en la base de datos (sin consultar Service Layer)
        /// Más confiable y rápido que validar en Service Layer
        /// </summary>
        public string ValidarBorradorEntregaExistente(string folio, string usuario)
        {
            try
            {
                log.Info($"Validando borrador de entrega existente para Folio: {folio}, Usuario: {usuario}");

                Dictionary<string, string> parametros = new Dictionary<string, string>
                {
                    { "Folio", folio },
                    { "Usuario", usuario }
                };

                string resultadoBD = GlobalCommands.ExecuteProcedure(AD.GCValidarDraftEntregaExistenteHHEMOV, parametros);

                if (string.IsNullOrEmpty(resultadoBD) || resultadoBD == "[]" || resultadoBD.Contains("Error"))
                {
                    log.Info("No se encontró borrador en BD");
                    return null;
                }

                JArray borradores = JArray.Parse(resultadoBD);
                if (borradores.Count == 0)
                {
                    log.Info("Consulta BD retornó 0 registros");
                    return null;
                }

                // ============================================================
                // Analizar resultado del stored procedure
                // ============================================================
                var borrador = borradores[0];

                string borradorDocEntry = borrador["Borrador"]?.ToString();
                string borradorDocEntryVerificado = borrador["BorradorDocEntry"]?.ToString();
                string tipoObjeto = borrador["TipoObjeto"]?.ToString();
                string estadoBorrador = borrador["EstadoBorrador"]?.ToString();

                log.Info($"Borrador encontrado en BD:");
                log.Info($"   - DocEntry: {borradorDocEntry}");
                log.Info($"   - DocEntry Verificado: {borradorDocEntryVerificado}");
                log.Info($"   - Tipo Objeto: {tipoObjeto}");
                log.Info($"   - Estado: {estadoBorrador}");

                // Validar que tenga DocEntry
                if (string.IsNullOrEmpty(borradorDocEntry))
                {
                    log.Warn("Borrador es null o vacío");
                    return null;
                }

                // Validar que exista en ODRF (DocEntry verificado no es null)
                if (string.IsNullOrEmpty(borradorDocEntryVerificado))
                {
                    log.Warn($"⚠️ Inconsistencia: Borrador {borradorDocEntry} existe en tabla PDX pero NO en ODRF");
                    log.Warn($"   Posiblemente fue convertido a entrega o eliminado");
                    return null;
                }

                // Validar que sea tipo Entrega (15)
                if (tipoObjeto != "15")
                {
                    log.Warn($"⚠️ El borrador {borradorDocEntry} NO es de tipo Entrega (ObjType: {tipoObjeto})");
                    return null;
                }

                // Validar que no esté cerrado
                if (estadoBorrador == "C")
                {
                    log.Warn($"⚠️ El borrador {borradorDocEntry} está CERRADO");
                    return null;
                }

                // ✅ Todo OK - El borrador existe y es válido
                log.Info($"✅ Borrador {borradorDocEntry} validado exitosamente (Tipo: {tipoObjeto}, Estado: {estadoBorrador})");

                return borradorDocEntry;
            }
            catch (Exception ex)
            {
                log.Error($"Error en ValidarBorradorEntregaExistente: {ex.Message}");
                log.Error($"StackTrace: {ex.StackTrace}");
                return null;
            }
        }
        public async Task<string> ValidarEntregaExistenteAsyncSL(string folio, string usuario)
        {
            try
            {
                log.Info($"Validando entrega existente para Folio: {folio}, Usuario: {usuario}");

                Dictionary<string, string> parametros = new Dictionary<string, string>
                {
                    { "Folio", folio },
                    { "Usuario", usuario }
                };

                string resultadoBD = GlobalCommands.ExecuteProcedure(AD.GCValidarEntregaExistenteHHEMOV, parametros);

                if (string.IsNullOrEmpty(resultadoBD) || resultadoBD == "[]" || resultadoBD.Contains("Error"))
                {
                    log.Info("No se encontró entrega en BD");
                    return null;
                }

                JArray entregas = JArray.Parse(resultadoBD);
                if (entregas.Count == 0)
                {
                    log.Info("Consulta BD retornó 0 registros");
                    return null;
                }

                string entregaDocNum = entregas[0]["EntregaMercancia"]?.ToString();
                string entregaDocEntry = entregas[0]["EntregaDocEntry"]?.ToString(); // ✅ DocEntry de la entrega

                log.Info($"Entrega encontrada - DocNum: {entregaDocNum}, DocEntry: {entregaDocEntry}");

                if (string.IsNullOrEmpty(entregaDocNum))
                {
                    log.Warn("EntregaMercancia es null o vacío");
                    return null;
                }

                // ============================================================
                // Validar en SAP Service Layer usando DocEntry de la entrega
                // ============================================================
                if (!string.IsNullOrEmpty(entregaDocEntry))
                {
                    try
                    {
                        var urlSAP = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes({entregaDocEntry})";
                        log.Info($"Consultando SAP con DocEntry de entrega: {urlSAP}");

                        var response = await LoginService._httpClient.GetAsync(urlSAP);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            dynamic dataSAP = JsonConvert.DeserializeObject(json);

                            string docNumSAP = dataSAP.DocNum?.ToString();
                            string canceladoSAP = dataSAP.Cancelled?.ToString();

                            log.Info($"✅ Entrega validada en SAP - DocNum: {docNumSAP}, Cancelado: {canceladoSAP}");

                            if (canceladoSAP == "Y" || canceladoSAP == "tYES")
                            {
                                log.Warn("La entrega está cancelada en SAP");
                                return null;
                            }

                            return entregaDocNum;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            log.Warn($"DocEntry {entregaDocEntry} no encontrado en SAP");
                            return null;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            log.Error($"Error SAP: {response.StatusCode} - {errorContent}");
                            return null;
                        }
                    }
                    catch (Exception exSAP)
                    {
                        log.Error($"Excepción al consultar SAP: {exSAP.Message}");
                        return null;
                    }
                }
                else
                {
                    log.Warn("No se encontró DocEntry de la entrega en BD");
                    return null;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error en ValidarEntregaExistenteAsync: {ex.Message}");
                return null;
            }
        }
        public async Task<string> ValidarBorradorEntregaExistenteAsyncSL(string folio, string usuario)
        {
            try
            {
                log.Info($"Validando borrador de entrega existente para Folio: {folio}, Usuario: {usuario}");

                Dictionary<string, string> parametros = new Dictionary<string, string>
                {
                    { "Folio", folio },
                    { "Usuario", usuario }
                };

                string resultadoBD = GlobalCommands.ExecuteProcedure(AD.GCValidarDraftEntregaExistenteHHEMOV, parametros);

                if (string.IsNullOrEmpty(resultadoBD) || resultadoBD == "[]" || resultadoBD.Contains("Error"))
                {
                    log.Info("No se encontró borrador en BD");
                    return null;
                }

                JArray borradores = JArray.Parse(resultadoBD);

                if (borradores.Count == 0)
                {
                    log.Info("Consulta BD retornó 0 registros");
                    return null;
                }

                string borradorDocNum = borradores[0]["Borrador"]?.ToString();
                string borradorDocEntry = borradores[0]["BorradorDocEntry"]?.ToString(); // ✅ DocEntry del borrador

                log.Info($"Borrador encontrado - DocNum: {borradorDocNum}, DocEntry: {borradorDocEntry}");

                if (string.IsNullOrEmpty(borradorDocNum))
                {
                    log.Warn("Borrador es null o vacío");
                    return null;
                }

                // ============================================================
                // Validar en SAP Service Layer usando DocEntry del borrador
                // ============================================================
                if (!string.IsNullOrEmpty(borradorDocEntry))
                {
                    try
                    {
                        var urlSAP = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Drafts({borradorDocEntry})";
                        log.Info($"Consultando SAP con DocEntry de borrador: {urlSAP}");

                        var response = await LoginService._httpClient.GetAsync(urlSAP);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            dynamic dataSAP = JsonConvert.DeserializeObject(json);

                            string docNumSAP = dataSAP.DocNum?.ToString();
                            string docObjectCode = dataSAP.DocObjectCode?.ToString();

                            log.Info($"✅ Borrador validado en SAP - DocNum: {docNumSAP}, DocObjectCode: {docObjectCode}");

                            // Verificar que sea un borrador de entrega (15 (oDeliveryNotes) = Delivery)
                            if (docObjectCode != "oDeliveryNotes")
                            {
                                log.Warn($"El borrador no es de tipo Entrega. DocObjectCode: {docObjectCode}");
                                return null;
                            }

                            return borradorDocNum;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            log.Warn($"DocEntry {borradorDocEntry} no encontrado en SAP (posiblemente ya fue convertido o eliminado)");
                            return null;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            log.Error($"Error SAP: {response.StatusCode} - {errorContent}");
                            return null;
                        }
                    }
                    catch (Exception exSAP)
                    {
                        log.Error($"Excepción al consultar SAP: {exSAP.Message}");
                        return null;
                    }
                }
                else
                {
                    log.Warn("No se encontró DocEntry del borrador en BD");
                    return null;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error en ValidarBorradorEntregaExistenteAsync: {ex.Message}");
                return null;
            }
        }
        private decimal CalcularDiscountPercentExacto(decimal cantidad, decimal precioUnitario, decimal totalDeseado)
        {
            // Calcula el descuento exacto
            decimal discountPercent = 100 * (1 - (totalDeseado / (cantidad * precioUnitario)));

            // Ajuste para evitar decimales residuales en SAP
            discountPercent = Math.Round(discountPercent, 8);

            return discountPercent;
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
                var sourceLine = ((JArray)orderData["DocumentLines"]).First;


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
                    // Normalizar variantes de kilogramos
                    if (UMI == "KGS" || UMI == "KILO" || UMI == "KILOS")
                    {
                        UMI = "KG";
                    }
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
                if (UMI == "KG" && UMS == "MTS")
                {
                    decimal cantidadMetros = totalCantidadConversion;
                    decimal cantidadYardas = cantidadMetros * ConversionYardas;
                    decimal precioPorMetro = decimal.Parse(sourceLine["U_Precioxmetro"].ToString());

                    // ✅ Calcular siempre
                    decimal lineTotalCorrecto = cantidadMetros * precioPorMetro;

                    decimal cantidadKG = totalCantidadNativa;

                    // Campos de usuario
                    newLine["U_Cantidadenmetros"] = cantidadMetros;
                    newLine["U_Cantidadenyardas"] = cantidadYardas;
                    newLine["U_Precioxmetro"] = precioPorMetro; // ✅ Guardar para referencia

                    newLine["Quantity"] = cantidadKG;

                    // ⭐ Lógica según tu descubrimiento
                    if (totalOV == lineTotalCorrecto) //✅
                    {
                        newLine["LineTotal"] = lineTotalCorrecto + 0.0001m;
                    }
                    else
                    {
                        // Totales diferentes → SAP respeta LineTotal
                        newLine["LineTotal"] = lineTotalCorrecto;
                    }
                }
                else if ((UMI == "KG" || UMI == "MTS") && UMS == "YDS")
                {
                    decimal cantidadMetros = totalCantidadConversion / ConversionYardas; // ✅ Conversión correcta
                    decimal cantidadYardas = totalCantidadConversion * ConversionYardas;
                    decimal precioPorYarda = decimal.Parse(sourceLine["U_Precioxyarda"].ToString());

                    decimal lineTotalCorrecto = cantidadYardas * precioPorYarda;
                    decimal cantidadNativa = totalCantidadNativa;

                    newLine["U_Cantidadenmetros"] = cantidadMetros;
                    newLine["U_Cantidadenyardas"] = cantidadYardas;
                    newLine["U_Precioxyarda"] = precioPorYarda;

                    newLine["Quantity"] = cantidadNativa;

                    // ⭐ Misma lógica
                    if (totalOV == lineTotalCorrecto)
                    {
                        newLine["LineTotal"] = lineTotalCorrecto + 0.0001m;
                    }
                    else
                    {
                        newLine["LineTotal"] = lineTotalCorrecto;
                    }
                }
                else
                {
                    // Caso estándar: mismas unidades
                    newLine["Quantity"] = totalCantidadNativa;
                    // SAP calculará automáticamente: Quantity × Price
                }
                // Campos obligatorios
                newLine["ItemCode"] = itemCode;

                // ✅ Como los lotes tienen distintos almacenes, solo ponemos el primero de referencia
                newLine["WarehouseCode"] = lotesParaLinea.First().WarehouseCode;
                newLine["BatchNumbers"] = batches;
                newLine["BaseType"] = 17;
                newLine["BaseEntry"] = RequestData.OrdenVenta;
                newLine["BaseLine"] = (int)sourceLine["LineNum"];
                newLine["UseBaseUnits"] = "tNO";

                // ------------------------------------------------------
                // ✅ AJUSTE FINAL DE LOTES
                // ------------------------------------------------------
                decimal sumaLotes = batches.Sum(b => b.Quantity);
                decimal cantidadLinea = Convert.ToDecimal(sourceLine["Quantity"]);
                decimal diferencia = Math.Round(sumaLotes - cantidadLinea, 2);
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
            try
            {
                log.Info("=======================================================================================");
                log.Info("=========================ACTUALIZAR LOTES ENTREGA======================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando actualización de lotes | OV: {(string)orderData.DocNum} | Cliente: {(string)orderData.CardCode} | Total Lotes: {LotesData.Count}");

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

                log.Info($"[INFO] Lotes agrupados por artículo y línea | Total Grupos: {lotesAgrupados.Count()}");

                int lotesProcesados = 0;
                int lotesConError = 0;

                // Recorrer los grupos
                foreach (var grupo in lotesAgrupados)
                {
                    string itemCode = grupo.ItemCode;
                    log.Info($"[INFO] Procesando grupo | ItemCode: {itemCode} | LineNum: {grupo.LineNum} | Cantidad de lotes en grupo: {grupo.Lotes.Count}");

                    foreach (var lote in grupo.Lotes)
                    {
                        string batchNumber = lote.BatchNumber;
                        string warehouseCode = lote.WarehouseCode;

                        try
                        {
                            log.Info($"[INFO] Actualizando lote | BatchNumber: {batchNumber} | ItemCode: {itemCode} | Almacen: {warehouseCode} | Cantidad: {lote.Cantidad}");

                            var loteUpdateParams = new Dictionary<string, string>
                            {
                                { "BatchNumber", batchNumber },
                                { "ItemCode", itemCode },
                                { "Fecha", RequestData.FechaEntrega.ToString("dd-MM-yyyy HH:mm:ss") },
                                { "OrdenVenta", (string)orderData.DocNum },
                                { "Cliente", (string)orderData.CardCode },
                                { "CantidadReferencia", string.Empty }
                            };

                            string resultado = GlobalCommands.ExecuteProcedure(AD.GCActualizaLoteVentasHHEMOV, loteUpdateParams);

                            if (string.IsNullOrEmpty(resultado) || resultado.Contains("Error"))
                            {
                                log.Error($"[ERROR] Error al actualizar lote | BatchNumber: {batchNumber} | ItemCode: {itemCode} | Resultado: {resultado}");
                                lotesConError++;
                            }
                            else
                            {
                                log.Info($"[OK] Lote actualizado exitosamente | BatchNumber: {batchNumber} | ItemCode: {itemCode}");
                                lotesProcesados++;
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[ERROR] Excepción al actualizar lote | BatchNumber: {batchNumber} | ItemCode: {itemCode} | Error: {ex.Message}");
                            lotesConError++;
                        }
                    }
                }

                log.Info($"[OK] Actualización de lotes completada | Total procesados: {lotesProcesados} | Con errores: {lotesConError} | OV: {(string)orderData.DocNum}");
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateLotesEntrega | OV: {(string)orderData.DocNum} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw;
            }
        }
        public void UpdateDocumentDelivery(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocNum)
        {
            try
            {
                log.Info("=======================================================================================");
                log.Info("======================ACTUALIZAR DOCUMENTO DELIVERY====================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando actualización de documento | FolioEM: {RequestData.FolioEM} | OV DocEntry: {RequestData.OrdenVenta} | DocNum Entrega: {DocNum}");

                var updateParams = new Dictionary<string, string>
        {
            { "OrdenVenta", RequestData.OrdenVenta.ToString() },
            { "Folio", RequestData.FolioEM },
            { "EntregaMercancia", DocNum },
            { "Borrador", "N/A" }
        };

                log.Info($"[INFO] Ejecutando procedimiento de actualización | Parámetros: OrdenVenta={RequestData.OrdenVenta}, Folio={RequestData.FolioEM}, EntregaMercancia={DocNum}, Borrador=N/A");

                string resultado = GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosVentasHHEMOV, updateParams);

                if (string.IsNullOrEmpty(resultado) || resultado.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al actualizar documento | FolioEM: {RequestData.FolioEM} | DocNum: {DocNum} | Resultado: {resultado}");
                }
                else
                {
                    log.Info($"[OK] Documento actualizado exitosamente | FolioEM: {RequestData.FolioEM} | DocNum: {DocNum}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateDocumentDelivery | FolioEM: {RequestData.FolioEM} | DocNum: {DocNum} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw;
            }
        }
        private async Task NotifyWebQuanqityRollos(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData)
        {
            try
            {
                log.Info("=======================================================================================");
                log.Info("====================NOTIFICAR WEB CANTIDAD ROLLOS=====================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando notificación a WEB | OV DocEntry: {RequestData.OrdenVenta} | Rollos: {RequestData.Rollos}");

                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, 4);
                    string url = ConfigurationManager.AppSettings["EndPointActualizacionRollos"];

                    log.Info($"[INFO] Preparando petición HTTP | Endpoint: {url} | OV: {RequestData.OrdenVenta} | Rollos: {RequestData.Rollos}");

                    var json = "{ \"pedido\": \"" + RequestData.OrdenVenta + "\", \"rollos\":" + RequestData.Rollos + "}";
                    var contentPedido = new StringContent(json, Encoding.UTF8, "application/json");

                    log.Info($"[INFO] Enviando petición POST | JSON: {json}");

                    HttpResponseMessage response = await client.PostAsync(url, contentPedido);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        log.Info($"[OK] Notificación enviada exitosamente a WEB | OV: {RequestData.OrdenVenta} | StatusCode: {response.StatusCode} | Respuesta: {responseBody}");
                    }
                    else
                    {
                        log.Error($"[ERROR] Error en respuesta de WEB | OV: {RequestData.OrdenVenta} | StatusCode: {response.StatusCode} | Respuesta: {responseBody}");
                    }
                }
            }
            catch (Exception e)
            {
                log.Error($"[ERROR] Excepción al notificar cantidad de rollos a WEB | OV DocEntry: {RequestData.OrdenVenta} | Rollos: {RequestData.Rollos} | Error: {e.Message} | StackTrace: {e.StackTrace}");
            }
        }

        private async Task NotifyWebEntrega(string OrdenVenta, string Entrega)
        {
            try
            {
                log.Info("=======================================================================================");
                log.Info("=========================NOTIFICAR WEB ENTREGA=========================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando notificación de entrega a WEB | OV DocEntry: {OrdenVenta} | Entrega DocEntry: {Entrega}");

                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, 4);
                    string url = ConfigurationManager.AppSettings["EndPointNuevaEntrega"];

                    log.Info($"[INFO] Preparando petición HTTP | Endpoint: {url} | OV: {OrdenVenta} | Entrega: {Entrega}");

                    var json = "{ \"DocEntryOV\": \"" + OrdenVenta + "\", \"DocEntryEntrega\":" + Entrega + "}";
                    var contentPedido = new StringContent(json, Encoding.UTF8, "application/json");

                    log.Info($"[INFO] Enviando petición POST | JSON: {json}");

                    HttpResponseMessage response = await client.PostAsync(url, contentPedido);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        log.Info($"[OK] Notificación de entrega enviada exitosamente a WEB | OV: {OrdenVenta} | Entrega: {Entrega} | StatusCode: {response.StatusCode} | Respuesta: {responseBody}");
                    }
                    else
                    {
                        log.Error($"[ERROR] Error en respuesta de WEB | OV: {OrdenVenta} | Entrega: {Entrega} | StatusCode: {response.StatusCode} | Respuesta: {responseBody}");
                    }
                }
            }
            catch (Exception e)
            {
                log.Error($"[ERROR] Excepción al notificar entrega a WEB | OV DocEntry: {OrdenVenta} | Entrega DocEntry: {Entrega} | Error: {e.Message} | StackTrace: {e.StackTrace}");
            }
        }
        private async Task UpdateMontoLetras(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocEntry)
        {
            try
            {
                log.Info("=======================================================================================");
                log.Info("=======================ACTUALIZAR MONTO EN LETRAS======================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando actualización de monto en letras | DocEntry Entrega: {DocEntry}");

                var deliveryUrlGet = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/DeliveryNotes({DocEntry})";

                log.Info($"[INFO] Obteniendo datos de la entrega desde SAP | URL: {deliveryUrlGet}");

                var deliveryResponse = await LoginService._httpClient.GetAsync(deliveryUrlGet);

                if (deliveryResponse.IsSuccessStatusCode)
                {
                    log.Info($"[OK] Datos de entrega obtenidos exitosamente | DocEntry: {DocEntry} | StatusCode: {deliveryResponse.StatusCode}");

                    var deliveryJson = await deliveryResponse.Content.ReadAsStringAsync();
                    dynamic deliveryData = JsonConvert.DeserializeObject(deliveryJson);

                    string docCurrency = (string)deliveryData.DocCurrency;

                    // DECIDIR QUÉ MONTO USAR: Si no es MXN, usar DocTotalFC
                    decimal totalFinal = (docCurrency != "MXN" && docCurrency != "MXP")
                        ? (decimal)deliveryData.DocTotalFc  // Moneda extranjera
                        : (decimal)deliveryData.DocTotal;   // Moneda local (MXN)

                    log.Info($"[INFO] Datos de moneda obtenidos | DocEntry: {DocEntry} | Moneda: {docCurrency} | Total: {totalFinal}");

                    string codigoMoneda = ObtenerCodigoMoneda(docCurrency);

                    log.Info($"[INFO] Convirtiendo monto a letras | DocEntry: {DocEntry} | Monto: {totalFinal} | Código Moneda: {codigoMoneda}");

                    string totalEnLetras = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "es").ToUpper();
                    string totalEnLetrasEN = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "en").ToUpper();

                    log.Info($"[INFO] Conversión completada | DocEntry: {DocEntry} | ES: {totalEnLetras.Substring(0, Math.Min(50, totalEnLetras.Length))}... | EN: {totalEnLetrasEN.Substring(0, Math.Min(50, totalEnLetrasEN.Length))}...");

                    var montoParams = new Dictionary<string, string>
                {
                    { "Entrega", DocEntry },
                    { "MontoES", totalEnLetras },
                    { "MontoEN", totalEnLetrasEN },
                    { "TipoDocumento", "ENTREGA" }
                };

                    log.Info($"[INFO] Ejecutando procedimiento de actualización | DocEntry: {DocEntry} | TipoDocumento: ENTREGA");

                    string result = GlobalCommands.ExecuteProcedure(AD.GCActualizaMontoLetrasHHEMOV, montoParams);

                    if (string.IsNullOrEmpty(result) || result.Contains("Error"))
                    {
                        log.Error($"[ERROR] Error al ejecutar procedimiento de actualización | DocEntry: {DocEntry} | Resultado: {result}");
                        throw new Exception($"Error al actualizar monto en letras: {result}");
                    }
                    else
                    {
                        log.Info($"[OK] Monto en letras actualizado exitosamente | DocEntry: {DocEntry}");
                    }
                }
                else
                {
                    var errorGet = await deliveryResponse.Content.ReadAsStringAsync();
                    log.Error($"[ERROR] Error al obtener datos de la entrega desde SAP | DocEntry: {DocEntry} | StatusCode: {deliveryResponse.StatusCode} | Error: {errorGet}");
                    throw new Exception(errorGet);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateMontoLetras, no es posible actualizar el monto en letras | DocEntry: {DocEntry} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw;
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
            try
            {
                log.Info($"[INFO] Iniciando actualización de lotes del borrador | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum} | Total lotes: {LotesData.Count}");

                // Agrupar solo por ItemCode y LineNum
                log.Info($"[INFO] Agrupando lotes por ItemCode y LineNum | Total lotes a procesar: {LotesData.Count}");

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

                int totalGrupos = lotesAgrupados.Count();
                log.Info($"[OK] Lotes agrupados exitosamente | Total grupos: {totalGrupos}");

                int grupoActual = 0;
                int lotesActualizados = 0;

                // Recorrer los grupos
                foreach (var grupo in lotesAgrupados)
                {
                    grupoActual++;
                    string itemCode = grupo.ItemCode;

                    log.Info($"[INFO] Procesando grupo {grupoActual}/{totalGrupos} | ItemCode: {itemCode} | LineNum: {grupo.LineNum} | Lotes en grupo: {grupo.Lotes.Count}");

                    foreach (var lote in grupo.Lotes)
                    {
                        try
                        {
                            string batchNumber = lote.BatchNumber;
                            string warehouseCode = lote.WarehouseCode;
                            string cantidadReferencia = lote.CantidadReferencia;

                            log.Info($"[INFO] Actualizando lote | BatchNumber: {batchNumber} | ItemCode: {itemCode} | Almacen: {warehouseCode} | CantidadRef: {cantidadReferencia}");

                            var loteUpdateParams = new Dictionary<string, string>
                    {
                        { "BatchNumber", batchNumber },
                        { "ItemCode", itemCode },
                        { "Fecha", RequestData.FechaEntrega.ToString("dd-MM-yyyy HH:mm:ss") },
                        { "OrdenVenta", (string)orderData.DocNum },
                        { "Cliente", (string)orderData.CardCode },
                        { "CantidadReferencia", cantidadReferencia } // específico para borrador
                    };

                            string resultado = GlobalCommands.ExecuteProcedure(AD.GCActualizaLoteVentasHHEMOV, loteUpdateParams);

                            if (!string.IsNullOrEmpty(resultado) && resultado.Contains("Error"))
                            {
                                log.Error($"[ERROR] Error al actualizar lote | BatchNumber: {batchNumber} | ItemCode: {itemCode} | Resultado: {resultado}");
                            }
                            else
                            {
                                lotesActualizados++;
                                log.Info($"[OK] Lote actualizado exitosamente | BatchNumber: {batchNumber} | ItemCode: {itemCode}");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[ERROR] Excepción al actualizar lote individual | BatchNumber: {lote.BatchNumber} | ItemCode: {itemCode} | Error: {ex.Message}");
                            // Continuar con los demás lotes aunque uno falle
                        }
                    }
                }

                log.Info($"[OK] Actualización de lotes del borrador completada | Total lotes actualizados: {lotesActualizados}/{LotesData.Count} | FolioEM: {RequestData.FolioEM}");
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateLotesEntregaDraft | FolioEM: {RequestData.FolioEM} | OV: {(string)orderData.DocNum} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw; // Re-lanzar la excepción para que el método llamador la maneje
            }
        }
        private void UpdateDocumentDraft(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocNum)
        {
            try
            {
                log.Info($"[INFO] Iniciando actualización de documento borrador | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta}");

                var updateParams = new Dictionary<string, string>
        {
            { "OrdenVenta", RequestData.OrdenVenta.ToString() },
            { "Folio", RequestData.FolioEM },
            { "EntregaMercancia", "N/A" }, // Específico para borrador
            { "Borrador", DocNum } // Específico para borrador
        };

                log.Info($"[INFO] Ejecutando procedimiento de actualización | Procedimiento: {AD.GCActualizaDocumentosVentasHHEMOV} | DocNum: {DocNum}");

                string resultado = GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosVentasHHEMOV, updateParams);

                if (!string.IsNullOrEmpty(resultado) && resultado.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al ejecutar procedimiento de actualización | DocNum: {DocNum} | Resultado: {resultado}");
                    throw new Exception($"Error al actualizar documento borrador: {resultado}");
                }

                log.Info($"[OK] Documento borrador actualizado exitosamente | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM}");
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción en UpdateDocumentDraft | DocNum: {DocNum} | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw; // Re-lanzar la excepción para que el método llamador la maneje
            }
        }
        private async Task UpdateMontoLetrasDraft(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData, string DocEntry)
        {
            try
            {
                log.Info($"[INFO] Iniciando actualización de monto en letras del borrador | DocEntry: {DocEntry} | FolioEM: {RequestData.FolioEM}");

                // Obtener el borrador completo para tener DocTotal Y DocCurrency
                log.Info($"[INFO] Obteniendo datos del borrador desde SAP | DocEntry: {DocEntry}");
                var draftUrlGet = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Drafts({DocEntry})";
                var draftResponse = await LoginService._httpClient.GetAsync(draftUrlGet);

                if (draftResponse.IsSuccessStatusCode)
                {
                    log.Info($"[OK] Borrador obtenido exitosamente desde SAP | DocEntry: {DocEntry} | StatusCode: {draftResponse.StatusCode}");

                    var draftJson = await draftResponse.Content.ReadAsStringAsync();

                    dynamic draftData;
                    try
                    {
                        draftData = JsonConvert.DeserializeObject(draftJson);
                        log.Info($"[OK] Datos del borrador deserializados correctamente | DocEntry: {DocEntry}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error al deserializar datos del borrador | DocEntry: {DocEntry} | Error: {ex.Message}");
                        throw new Exception($"Error al procesar datos del borrador: {ex.Message}");
                    }

                    string docCurrency = (string)draftData.DocCurrency; // 👈 OBTENER LA MONEDA DEL BORRADOR
                    log.Info($"[INFO] Moneda del documento obtenida | DocEntry: {DocEntry} | Moneda: {docCurrency}");

                    // Por esto:
                    decimal totalFinal = (docCurrency != "MXN" && docCurrency != "MXP")
                        ? (decimal)draftData.DocTotalFc
                        : (decimal)draftData.DocTotal;

                    log.Info($"[INFO] Total calculado | DocEntry: {DocEntry} | Total: {totalFinal} | Moneda: {docCurrency}");

                    // Determinar el código de moneda para la conversión
                    log.Info($"[INFO] Obteniendo código de moneda para conversión | Moneda: {docCurrency}");
                    string codigoMoneda = ObtenerCodigoMoneda(docCurrency);
                    log.Info($"[OK] Código de moneda obtenido | Moneda Original: {docCurrency} | Código: {codigoMoneda}");

                    // Convertir a letras (USANDO la moneda real del documento)
                    log.Info($"[INFO] Convirtiendo monto a letras | Monto: {totalFinal} | Moneda: {codigoMoneda}");
                    string totalEnLetras = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "es").ToUpper(); // Español
                    string totalEnLetrasEN = NumerosALetras.ConvertirConMoneda(totalFinal, codigoMoneda, "en").ToUpper(); // Inglés

                    log.Info($"[OK] Monto convertido a letras | ES: {totalEnLetras.Substring(0, Math.Min(50, totalEnLetras.Length))}... | EN: {totalEnLetrasEN.Substring(0, Math.Min(50, totalEnLetrasEN.Length))}...");

                    // -----------------------------
                    // LLAMAR STORED PROCEDURE PARA ACTUALIZAR UDFs
                    // -----------------------------
                    log.Info($"[INFO] Preparando parámetros para actualización de monto en letras | DocEntry: {DocEntry}");
                    var montoParams = new Dictionary<string, string>
                    {
                        { "Entrega", DocEntry },
                        { "MontoES", totalEnLetras },
                        { "MontoEN", totalEnLetrasEN },
                        { "TipoDocumento", "BORRADOR" }
                    };

                    log.Info($"[INFO] Ejecutando procedimiento de actualización de monto | Procedimiento: {AD.GCActualizaMontoLetrasHHEMOV} | DocEntry: {DocEntry}");
                    string result = GlobalCommands.ExecuteProcedure(AD.GCActualizaMontoLetrasHHEMOV, montoParams);

                    // Opcional: Validar el resultado del stored procedure
                    if (result.Contains("Error"))
                    {
                        log.Error($"[ERROR] Error en procedimiento de actualización de monto | DocEntry: {DocEntry} | Resultado: {result}");
                        throw new Exception($"Error al actualizar monto en letras: {result}");
                    }
                    else if (!string.IsNullOrEmpty(result))
                    {
                        log.Warn($"[WARN] Procedimiento retornó mensaje | DocEntry: {DocEntry} | Resultado: {result}");
                    }
                    else
                    {
                        log.Info($"[OK] Monto en letras actualizado exitosamente | DocEntry: {DocEntry}");
                    }
                }
                else
                {
                    var errorGet = await draftResponse.Content.ReadAsStringAsync();
                    log.Error($"[ERROR] No se pudo obtener el borrador desde SAP | DocEntry: {DocEntry} | StatusCode: {draftResponse.StatusCode} | Error: {errorGet}");
                    throw new Exception($"Error al obtener borrador para monto en letras: {errorGet}");
                }

                log.Info($"[OK] Proceso de actualización de monto en letras completado exitosamente | DocEntry: {DocEntry}");
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateMontoLetrasDraft | DocEntry: {DocEntry} | FolioEM: {RequestData.FolioEM} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                throw; // Re-lanzar la excepción para que el método llamador la maneje
            }
        }
        //Notificacion a facturacion
        public bool NotificacionFacturacion(string Asunto, string Titulo, string AsuntoHtml, string Mensaje, string OrdenVenta, string Area)
        {
            try
            {
                log.Info("=======================================================================================");
                log.Info("=======================NOTIFICACIÓN FACTURACIÓN========================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Iniciando envío de notificación | Área: {Area} | OrdenVenta: {OrdenVenta} | Asunto: {Asunto}");

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

                log.Info($"[INFO] Plantilla HTML generada | OrdenVenta: {OrdenVenta}");

                // Email el mensaje de correo
                var Email = new MailMessage
                {
                    From = new MailAddress(ConfigurationManager.AppSettings["EmailFrom"], "Entregas"),
                    Subject = Asunto,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                log.Info($"[INFO] Configurando recursos embebidos | OrdenVenta: {OrdenVenta}");

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

                log.Info($"[INFO] Configurando servidor SMTP | Host: {ConfigurationManager.AppSettings["EmailHost"]} | Port: {ConfigurationManager.AppSettings["EmailPort"]}");

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
                log.Info($"[INFO] Obteniendo destinatarios | Área: {Area} | OrdenVenta: {OrdenVenta}");

                JArray Emails = JArray.Parse((Area == "Facturación" ? EmailsFacturacion : EmailsVentas));

                int destinatariosAgregados = 0;
                foreach (JObject email in Emails)
                {
                    var direcciones = email["Email"].ToString().Split(',');
                    foreach (var direccion in direcciones)
                    {
                        if (!string.IsNullOrWhiteSpace(direccion))
                        {
                            Email.To.Add(direccion.Trim());
                            destinatariosAgregados++;
                            log.Info($"[INFO] Destinatario agregado: {direccion.Trim()}");
                        }
                    }
                }

                log.Info($"[INFO] Total destinatarios agregados: {destinatariosAgregados} | OrdenVenta: {OrdenVenta}");

                if (destinatariosAgregados == 0)
                {
                    log.Warn($"[WARN] No se agregaron destinatarios al correo | Área: {Area} | OrdenVenta: {OrdenVenta}");
                    return false;
                }

                log.Info($"[INFO] Enviando correo | Destinatarios: {destinatariosAgregados} | OrdenVenta: {OrdenVenta}");

                smtpMail.Send(Email);

                log.Info($"[OK] Correo enviado exitosamente | Área: {Area} | OrdenVenta: {OrdenVenta} | Destinatarios: {destinatariosAgregados}");

                smtpMail.Dispose();
                Email.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en NotificacionFacturacion, no pudo ser enviada la notificación | Área: {Area} | OrdenVenta: {OrdenVenta} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        #endregion
    }
}