using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using static RESTAPIFFISA.AccesoDatosInventarios;


namespace RESTAPIFFISA
{
    public class LogicaInventariosMovil
    {
        #region variables
        public AccesoDatosInventarios AD = new AccesoDatosInventarios();
        public LoginServiceLayer LoginService = new LoginServiceLayer();
        public GlobalCommands GlobalCommands = new GlobalCommands();
        private static readonly ILog log = LogManager.GetLogger("InventariosMovil");
        List<string> Impresoras { get; set; }

        #endregion

        #region SBO INVENTARIOS
        // Método para crear una entrada de mercancía directa a inventario
        public async Task<GlobalCommands.SapResponse> CreateGoodsReceiptAsync(AccesoDatosInventarios.EntradasMercanciaHHEMD RequestData)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };
            Dictionary<string, string> keys = new Dictionary<string, string>();
            try
            {
                keys.Add("Usuario", RequestData.Usuario);
                string ImpresorasRed = GlobalCommands.ExecuteProcedure(GlobalCommands.GCGetImpresorasSeleccionadas, keys);
                if (ImpresorasRed.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener la lista de impresoras para la impresión de la etiqueta.";
                    return responseAbx;
                }

                JArray Print = JArray.Parse(ImpresorasRed);
                if (Print.Count == 0)
                {
                    responseAbx.Message = $"No tienes seleccionada ninguna impresora para imprimir, por favor seleccione la impresora a utilizar el el menú PRINCIPAL e intenta de nuevo. ";
                    return responseAbx;
                }
                Impresoras = Print[0]["Impresoras"].ToString().Split(',').ToList();
                keys.Clear();

                dynamic goodsReceipt = new ExpandoObject();
                var dictGR = (IDictionary<string, object>)goodsReceipt;
                Dictionary<string, string> PlantillaData = ImprimirPlantilla(RequestData.Usuario);
                string PlantillaEtiqueta = PlantillaData["PLANTILLA"];
                string ZPL = PlantillaData["ZPL"];
                //Si no fue posible obtener la olantilla
                if (ZPL.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener información de la plantilla seleccionada para la impresión de etiquetas.";
                    return responseAbx;
                }
                if (ZPL.Contains("SIN PLANTILLA"))
                {
                    responseAbx.Message = $"No se cuenta con ninguna plantilla de impresion seleccionada, por favor seleccione la etiqueta a utilizar el el menú de INVENTARIOS e intenta de nuevo.";
                    return responseAbx;
                }
                // Campos estándar
                dictGR["DocDate"] = RequestData.FechaEntrada.ToString("yyyy-MM-dd");
                dictGR["DocDueDate"] = RequestData.FechaEntrada.ToString("yyyy-MM-dd");
                dictGR["Comments"] = $"Entrada de mercancía para el folio: {RequestData.FolioEM}";
                dictGR["JournalMemo"] = $"Entrada de mercancía para el folio: {RequestData.FolioEM}";

                // UDFs de cabecera si aplica
                dictGR["U_UsuIniSes"] = RequestData.Usuario;

                // Líneas
                dictGR["DocumentLines"] = new List<object>();

                // Obtener los artículos registrados desde el SP

                keys.Add("Folio", RequestData.FolioEM);
                keys.Add("Usuario", RequestData.Usuario);
                string articulos = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosEntradasHHEMD, keys);

                if (articulos == "[]")
                {
                    responseAbx.Message = $"No se encontró información de artículos asociada al folio {RequestData.FolioEM}";
                    return responseAbx;
                }
                else if (articulos.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener información de los artículos asociada al folio {RequestData.FolioEM}: {articulos}";
                    return responseAbx;
                }

                JArray ArticulosData = JArray.Parse(articulos);

                foreach (var linea in ArticulosData)
                {

                    var newLine = new ExpandoObject() as IDictionary<string, object>;
                    newLine["ItemCode"] = linea["Articulo"].ToString();
                    newLine["Quantity"] = Convert.ToDouble(linea["Cantidad"]);
                    newLine["WarehouseCode"] = linea["Almacen"].ToString();


                    // ✅ UDFs de línea (ejemplos, ajusta a tus campos reales)
                    newLine["U_Cantidadenmetros"] = (string)(linea["CantidadMetros"]);

                    // ✅ Cuenta contable (FormatCode de OACT que ya trae el SP)
                    if (!string.IsNullOrWhiteSpace(linea["CodigoCuentaContable"]?.ToString()))
                        newLine["AccountCode"] = (string)(linea["CodigoCuentaContable"]);

                    string lote = string.Empty;
                    // Manejo de lotes
                    if (linea["Lote"] != null && !string.IsNullOrWhiteSpace(linea["Lote"].ToString()))
                    {
                        lote = linea["Lote"].ToString();
                    }
                    else
                    {
                        if (PlantillaEtiqueta.Contains("MATERIA PRIMA"))
                            lote = GenerarLoteMP((string)linea["Articulo"], (PlantillaEtiqueta.Contains("PACAS") ? "pacas" : "tubos"), RequestData.FechaEntrada.ToString("ddMMyy"));
                        else
                            lote = GenerarLote((string)linea["Articulo"], RequestData.Usuario, RequestData.FolioEM);

                        //Actualizar a nivel linea el lote
                        keys.Clear();
                        keys.Add("ID", (string)linea["ID"]);
                        keys.Add("Lote", lote);
                        keys.Add("Cantidadenmetros", (string)linea["CantidadMetros"]);
                        string NewLote = GlobalCommands.ExecuteProcedure(AD.GCActualizaLinesDocumentosEntradasHHEMD, keys);

                        if (NewLote == "[]")
                        {
                            responseAbx.Message = $"No se encontró información de artículos asociada al folio, No fue posible generar nuevos lotes para el artículo {linea["Articulo"]}";
                            return responseAbx;
                        }
                        else if (NewLote.Contains("Error"))
                        {
                            responseAbx.Message = $"No fue posible generar nuevos lotes para el artículo {linea["Articulo"]} {NewLote}";
                            return responseAbx;
                        }
                    }

                    var batchList = new List<object>
                    {
                        new
                        {
                            BatchNumber = lote,
                            Quantity = Convert.ToDouble(linea["Cantidad"])
                        }
                    };

                    newLine["BatchNumbers"] = batchList;

                    ((List<object>)dictGR["DocumentLines"]).Add(newLine);
                }


                // Paso 2: Enviar la entrada de mercancía a SAP
                var grUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryGenEntries";
                var jsonBody = JsonConvert.SerializeObject(goodsReceipt);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(grUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    responseAbx.IsError = false;
                    string DocNum = created.DocNum;
                    //Fecha de contabilizacion
                    DateTime fechaCont = created.DocDate;
                    string diaContabilizacion = fechaCont.ToString("dd");
                    string mesContabilizacion = fechaCont.ToString("MM");

                    responseAbx.Message = $"Entrada de mercancía creada exitosamente. Folio: {DocNum}";


                    // 🔹 ACTUALIZAR LOTES CON CANTIDAD EN METROS Y COMENTARIOS
                    keys.Clear();
                    keys.Add("Folio", RequestData.FolioEM);
                    keys.Add("Usuario", RequestData.Usuario);
                    articulos = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosEntradasHHEMD, keys);
                    ArticulosData = JArray.Parse(articulos);
                    foreach (var linea in ArticulosData)
                    {
                        ZPL = PlantillaData["ZPL"];
                        string[] LoteParts = new string[2];
                        LoteParts = linea["Lote"].ToString().Split('/');
                        string ItemCode = linea["Articulo"].ToString();
                        string BatchNumber = LoteParts[0];
                        string BatchRecalculado = (LoteParts.Length > 1 ? LoteParts[1] : string.Empty);
                        string CantidadMetros = linea["CantidadMetros"].ToString();
                        string Kilos = linea["Cantidad"].ToString();
                        string Comentarios = linea["Comentarios"].ToString();
                        string ExisteEtiqueta = linea["Etiqueta"].ToString();

                        var loteUpdateParams = new Dictionary<string, string>
                            {
                                { "BatchNumber", BatchNumber },  // asigna aquí el valor real
                                { "ItemCode", ItemCode },
                                { "Cantidadenmetros", CantidadMetros },
                                { "Comentarios", Comentarios},
                            };

                        GlobalCommands.ExecuteProcedure(AD.GCActualizaLoteEntradasHHEMD, loteUpdateParams);


                        //Mandar Imprimir
                        keys.Clear();
                        keys.Add("DistNumber", BatchNumber);
                        string Etiqueta = GlobalCommands.ExecuteProcedure(AD.GCObtenerDatosEtiquetaHHEMD, keys);
                        JArray DatosEtiqueta = JArray.Parse(Etiqueta);
                        if (DatosEtiqueta.Count > 0)
                        {
                            //Generar Numero de Pieza
                            StringBuilder NumeroPieza = new StringBuilder();
                            NumeroPieza.AppendLine("L");
                            NumeroPieza.AppendLine((string)DatosEtiqueta[0]["LineaProduccion"]);
                            NumeroPieza.AppendLine(diaContabilizacion);
                            NumeroPieza.AppendLine(mesContabilizacion);
                            string ConsecutivoGlobalLote = GetNumeroLote(BatchNumber);
                            NumeroPieza.AppendLine(ConsecutivoGlobalLote);

                            // Recorremos cada objeto en el JArray
                            foreach (JObject EtiquetaInfo in DatosEtiqueta)
                            {
                                // Recorremos todas las propiedades del objeto
                                EtiquetaInfo.Properties().ToList().ForEach(prop =>
                                {
                                    string nombrePropiedad = prop.Name;       // Ej: "Nombre"
                                    string valorPropiedad = prop.Value.ToString(); // Ej: "Juan"

                                    // Reemplazamos el placeholder en la cadena ZPL
                                    ZPL = ZPL.Replace($"{{{nombrePropiedad}}}", valorPropiedad);
                                });
                            }

                            ZPL = ZPL.Replace("{NumeroPieza}", NumeroPieza.ToString());
                            ZPL = ZPL.Replace("{CodigoBarras}", BatchNumber);
                            ZPL = ZPL.Replace("{Fecha}", RequestData.FechaEntrada.ToString("yyyy-MM-dd"));
                            ZPL = ZPL.Replace("{Kilos}", Kilos);


                            keys.Clear();


                            //Quiere decir que se imprime nueva etiqueta
                            if (ExisteEtiqueta == "NO")
                            {
                                foreach (string printer in Impresoras)
                                {
                                    //Enviar a impresora
                                    string Impresion = EnviarZPLConValidacion(printer, ZPL);

                                    if (Impresion != "OK")
                                    {
                                        responseAbx.Message += Environment.NewLine + Environment.NewLine +
                                                     $"● No es posible imprimir la etiqueta para el lote {BatchNumber} : " + Impresion;
                                    }
                                    else
                                    {
                                        if (BatchRecalculado == "recalculado")
                                            responseAbx.Message += Environment.NewLine + Environment.NewLine +
                                                                     $"● Se detectó que el lote ya existe y se imprimió etiqueta con nuevo consecutivo: {BatchNumber}";
                                        else
                                            responseAbx.Message += Environment.NewLine + Environment.NewLine +
                                                             $"● Etiqueta impresa con lote: {BatchNumber}";
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!responseAbx.Message.Contains(ItemCode))
                                responseAbx.Message += Environment.NewLine + Environment.NewLine +
                                                 $"● Considerar que el artículo {ItemCode} no esta gestionado por lotes, por lo cual no se imprime etiqueta.";
                        }
                    }

                    // Actualizar tablas internas
                    var updateParams = new Dictionary<string, string>
                    {
                        { "Folio", RequestData.FolioEM },
                        { "EntradaMercancia", DocNum }
                    };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosEntradasHHEMD, updateParams);

                }
                else
                {
                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;

                        responseAbx.Message = $"{errorCode} {errorMessage}";
                    }
                    catch
                    {
                        responseAbx.Message = $"{postResult}";
                    }

                    //Regresar los lotes al estado original 
                    foreach (var linea in ArticulosData)
                    {
                        string ItemCode = linea["Articulo"].ToString();
                        //Actualizar a nivel linea el lote
                        keys.Clear();
                        keys.Add("ID", (string)linea["ID"]);
                        keys.Add("Lote", string.Empty);
                        keys.Add("Cantidadenmetros", string.Empty);
                        string NewLote = string.Empty;

                        if ((string)linea["Etiqueta"] == "NO")
                        {
                            NewLote = GlobalCommands.ExecuteProcedure(AD.GCActualizaLinesDocumentosEntradasHHEMD, keys);
                            //Para materia prima
                            if (PlantillaEtiqueta.Contains("MATERIA PRIMA"))
                            {
                                //Ruta del archivo JSON
                                string filePath = ConfigurationManager.AppSettings["ConsecutivosEntradasDirectasMP"];
                                //Número consecutivo
                                ConsecutivoInfoMP NuevoConsecutivo = new ConsecutivoInfoMP();
                                // Leer los consecutivos desde el archivo JSON
                                var consecutivos = LeerConsecutivosMP(filePath);

                                if ((PlantillaEtiqueta.Contains("TUBOS")))
                                {
                                    NuevoConsecutivo = consecutivos.Tubos;
                                    NuevoConsecutivo.Consecutivo = ((NuevoConsecutivo.Consecutivo - 1) < 0 ? 0 : (NuevoConsecutivo.Consecutivo - 1));
                                    consecutivos.Tubos = NuevoConsecutivo;
                                }
                                else if ((PlantillaEtiqueta.Contains("PACAS")))
                                {
                                    NuevoConsecutivo = consecutivos.Pacas;
                                    NuevoConsecutivo.Consecutivo = ((NuevoConsecutivo.Consecutivo - 1) < 0 ? 0 : (NuevoConsecutivo.Consecutivo - 1));
                                    consecutivos.Pacas = NuevoConsecutivo;
                                }

                                GuardarConsecutivos(filePath, consecutivos);
                            }
                            else
                            {
                                //Si el consecutivo si fue generado y aun asi no se pudo generar la entrada 
                                //Disminuir al consecutivo siempre y cuando no sea 0
                                //Ruta del archivo JSON
                                var PlantillaParametros = new Dictionary<string, string>
                        {
                            { "Plantilla", PlantillaEtiqueta },
                            { "ItemCode", ItemCode }
                        };

                                string DatosCB = GlobalCommands.ExecuteProcedure(AD.GCGetNomenclaturaEtiqueta, PlantillaParametros);
                                JArray BarCodeData = JArray.Parse(DatosCB);

                                //Ruta del archivo JSON
                                string filePath = string.Empty;
                                switch (BarCodeData[0]["U_IdFecha"].ToString())
                                {
                                    case "Semanal":
                                        filePath = ConfigurationManager.AppSettings["ConsecutivosEDSemanal"];
                                        break;
                                    case "Mensual":
                                        filePath = ConfigurationManager.AppSettings["ConsecutivosEDMensual"];
                                        break;
                                    case "Semestral":
                                        filePath = ConfigurationManager.AppSettings["ConsecutivosEDSemestral"];
                                        break;
                                    case "Anual":
                                        filePath = ConfigurationManager.AppSettings["ConsecutivosEDAnuales"];
                                        break;
                                }
                                //Número consecutivo
                                ConsecutivoInfoED NuevoConsecutivo = new ConsecutivoInfoED();
                                // Leer los consecutivos desde el archivo JSON
                                var consecutivos = LeerConsecutivos(filePath);
                                NuevoConsecutivo.Consecutivo = consecutivos.EntradasDirectas.Consecutivo;
                                NuevoConsecutivo.Consecutivo = ((NuevoConsecutivo.Consecutivo - 1) < 0 ? 0 : (NuevoConsecutivo.Consecutivo - 1));
                                // Guardar los cambios en el archivo JSON
                                consecutivos.EntradasDirectas.Consecutivo = NuevoConsecutivo.Consecutivo;
                                GuardarConsecutivos(filePath, consecutivos);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            return responseAbx;
        }
        // Método para crear una salida de mercancía directa a inventario
        public async Task<GlobalCommands.SapResponse> CreateGoodsIssueAsync(AccesoDatosInventarios.SalidasMercanciaHHEMD RequestData)
        {
            var responseAbx = new GlobalCommands.SapResponse()
            {
                IsError = true
            };
            Dictionary<string, string> keys = new Dictionary<string, string>();
            try
            {
                dynamic goodsIssue = new ExpandoObject();
                var dictGI = (IDictionary<string, object>)goodsIssue;

                // Campos estándar
                dictGI["DocDate"] = RequestData.FechaSalida.ToString("yyyy-MM-dd");
                dictGI["DocDueDate"] = RequestData.FechaSalida.ToString("yyyy-MM-dd");
                dictGI["Comments"] = $"Salida de mercancía para el folio: {RequestData.FolioSM}";
                dictGI["JournalMemo"] = $"Salida de mercancía para el folio: {RequestData.FolioSM}";

                // UDFs de cabecera si aplica
                dictGI["U_UsuIniSes"] = RequestData.Usuario;

                // Líneas
                dictGI["DocumentLines"] = new List<object>();

                // Obtener artículos del SP
                keys.Add("Folio", RequestData.FolioSM);
                keys.Add("Usuario", RequestData.Usuario);
                string articulos = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosSalidasHHEMD, keys);

                if (articulos == "[]")
                {
                    responseAbx.Message = $"No se encontró información de artículos asociada al folio {RequestData.FolioSM}";
                    return responseAbx;
                }
                else if (articulos.Contains("Error"))
                {
                    responseAbx.Message = $"No fue posible obtener información de los artículos asociada al folio {RequestData.FolioSM}: {articulos}";
                    return responseAbx;
                }

                JArray ArticulosData = JArray.Parse(articulos);

                foreach (var linea in ArticulosData)
                {
                    var newLine = new ExpandoObject() as IDictionary<string, object>;
                    newLine["ItemCode"] = linea["Articulo"].ToString();
                    newLine["Quantity"] = Convert.ToDouble(linea["Cantidad"]);
                    newLine["WarehouseCode"] = linea["Almacen"].ToString();

                    // ✅ UDFs de línea
                    newLine["U_Cantidadenmetros"] = (string)(linea["CantidadMetros"]);
                    newLine["U_Depto_Solicitante"] = "ADMINISTRATIVO";

                    // ✅ Cuenta contable si aplica
                    if (!string.IsNullOrWhiteSpace(linea["CodigoCuentaContable"]?.ToString()))
                        newLine["AccountCode"] = (string)(linea["CodigoCuentaContable"]);

                    // ✅ Manejo de lotes (igual que en la entrada, pero ahora los consume)
                    if (linea["Lote"] != null && !string.IsNullOrWhiteSpace(linea["Lote"].ToString()))
                    {
                        var batchList = new List<object>
                {
                    new
                    {
                        BatchNumber = linea["Lote"].ToString(),
                        Quantity = Convert.ToDouble(linea["Cantidad"])
                    }
                };
                        newLine["BatchNumbers"] = batchList;
                    }

                    ((List<object>)dictGI["DocumentLines"]).Add(newLine);
                }

                // Paso 2: Enviar la salida de mercancía a SAP
                var giUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryGenExits";
                var jsonBody = JsonConvert.SerializeObject(goodsIssue);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(giUrl, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    responseAbx.IsError = false;
                    string DocNum = created.DocNum;
                    responseAbx.Message = $"Salida de mercancía creada exitosamente. Folio: {DocNum}";

                    // Actualizar tablas internas
                    var updateParams = new Dictionary<string, string>
                    {
                        { "Folio", RequestData.FolioSM },
                        { "SalidaMercancia", DocNum }
                    };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosSalidasHHEMD, updateParams);
                }
                else
                {
                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;

                        responseAbx.Message = $"{errorCode} {errorMessage}";
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
        // Método para crear una transferenica de mercancía directa a inventario
        public async Task<GlobalCommands.SapResponse> CreateTransferAsync(AccesoDatosInventarios.TraspasosMercanciaHHEMD RequestData)
        {
            var response = new GlobalCommands.SapResponse()
            {
                IsError = true
            };
            Dictionary<string, string> keys = new Dictionary<string, string>();

            try
            {
                dynamic transfer = new ExpandoObject();
                var dictTR = (IDictionary<string, object>)transfer;

                // Cabecera
                dictTR["DocDate"] = DateTime.Now.ToString("yyyy-MM-dd");
                dictTR["TaxDate"] = DateTime.Now.ToString("yyyy-MM-dd");
                dictTR["Comments"] = $"Traspaso de mercancía para el folio: {RequestData.FolioTM}";
                dictTR["JournalMemo"] = $"Traspaso de mercancía para el folio: {RequestData.FolioTM}";

                // UDFs de cabecera si aplica
                dictTR["U_UsuIniSes"] = RequestData.Usuario;

                // Líneas
                dictTR["StockTransferLines"] = new List<object>();

                // Obtener artículos desde tu SP
                keys.Add("Folio", RequestData.FolioTM);
                keys.Add("Usuario", RequestData.Usuario);
                string articulos = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosTransferenciasHHTS, keys);

                if (articulos == "[]")
                {
                    response.Message = $"No se encontró información de artículos asociada al folio {RequestData.FolioTM}";
                    return response;
                }
                else if (articulos.Contains("Error"))
                {
                    response.Message = $"No fue posible obtener información de artículos asociada al folio {RequestData.FolioTM}: {articulos}";
                    return response;
                }

                JArray ArticulosData = JArray.Parse(articulos);

                foreach (var linea in ArticulosData)
                {
                    var newLine = new ExpandoObject() as IDictionary<string, object>;
                    newLine["ItemCode"] = linea["CodigoArticulo"].ToString();
                    newLine["Quantity"] = Convert.ToDouble(linea["Cantidad"]);
                    newLine["FromWarehouseCode"] = linea["CodigoAlmacenOrigen"].ToString();
                    newLine["WarehouseCode"] = linea["CodigoAlmacenDestino"].ToString();

                    // ✅ UDFs de línea si necesitas
                    //if (linea["Comentarios"] != null)
                    //    newLine["U_Comentarios"] = linea["Comentarios"].ToString();

                    // ✅ Manejo de lotes
                    if (linea["LoteOrigen"] != null && !string.IsNullOrWhiteSpace(linea["LoteOrigen"].ToString()))
                    {
                        var batchList = new List<object>
                        {
                            new
                            {
                                BatchNumber = linea["LoteOrigen"].ToString(),
                                Quantity = Convert.ToDouble(linea["Cantidad"])
                            }
                        };
                        newLine["BatchNumbers"] = batchList;
                    }

                    ((List<object>)dictTR["StockTransferLines"]).Add(newLine);
                }

                // Enviar traspaso a SAP
                var url = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/StockTransfers";
                var jsonBody = JsonConvert.SerializeObject(transfer);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var postResponse = await LoginService._httpClient.PostAsync(url, content);
                var postResult = await postResponse.Content.ReadAsStringAsync();

                if (postResponse.IsSuccessStatusCode)
                {
                    dynamic created = JsonConvert.DeserializeObject(postResult);
                    response.IsError = false;
                    string DocNum = created.DocNum;
                    response.Message = $"Traspaso creado exitosamente. Documento SAP: {DocNum}";

                    // Actualizar tablas internas
                    var updateParams = new Dictionary<string, string>
                        {
                            { "Folio", RequestData.FolioTM },
                            { "TransferenciaStock", DocNum }
                        };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosTraspasosHHEMD, updateParams);
                }
                else
                {
                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(postResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;

                        response.Message = $"{errorCode} {errorMessage}";
                    }
                    catch
                    {
                        response.Message = $"{postResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = $"Excepción: {ex.Message}";
            }

            return response;
        }

        public async Task<GlobalCommands.SapResponse> UpdateInventoryCountingAsync(AccesoDatosInventarios.RecuentoInventarioHHRI RequestData)
        {
            var response = new GlobalCommands.SapResponse()
            {
                IsError = true
            };

            try
            {
                log.Info("=======================================================================================");
                log.Info("======================ACTUALIZAR RECUENTO INVENTARIO SAP===============================");
                log.Info("=======================================================================================");
                log.Info($"[OK] Inicia actualización de recuento en SAP | Usuario: {RequestData.Usuario} | DocEntry: {RequestData.DocEntry} | FolioRI: {RequestData.FolioRI}");

                Dictionary<string, string> keys = new Dictionary<string, string>();
                keys.Add("Folio", RequestData.FolioRI);
                keys.Add("Usuario", RequestData.Usuario);

                // 🔹 Obtener artículos totales por docentry
                string articulos = GlobalCommands.ExecuteProcedure(AD.GCGetLinesDocumentosRecuentosGroupedHHRI, keys);

                if (articulos == "[]")
                {
                    response.Message = $"No se encontró información de artículos asociada al folio {RequestData.FolioRI}";
                    log.Error($"[ERROR] No se encontraron líneas para el folio | Usuario: {RequestData.Usuario} | FolioRI: {RequestData.FolioRI}");
                    return response;
                }
                else if (articulos.Contains("Error"))
                {
                    response.Message = $"Error al obtener líneas del folio {RequestData.FolioRI}: {articulos}";
                    log.Error($"[ERROR] Error al obtener líneas desde BD | Usuario: {RequestData.Usuario} | FolioRI: {RequestData.FolioRI} | Error: {articulos}");
                    return response;
                }

                JArray ArticulosData = JArray.Parse(articulos);
                decimal totalCantidadEscaneada = ArticulosData.Sum(x => x["Cantidad"] != null
                    ? Convert.ToDecimal(x["Cantidad"])
                    : 0);

                log.Info($"[OK] Se obtuvieron {ArticulosData.Count} registros para procesar | FolioRI: {RequestData.FolioRI}");


                // 🔹 Separar artículos existentes vs nuevos
                var articulosExistentes = ArticulosData
                    .Where(a => a["ExisteEnRecuento"]?.ToString().ToUpper() == "SI")
                    .ToList();

                var articulosNuevos = ArticulosData
                    .Where(a => a["ExisteEnRecuento"]?.ToString().ToUpper() == "NO")
                    .ToList();

                // 🔹 1. GET del recuento en SAP
                var getUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryCountings({RequestData.DocEntry})";
                var getResponse = await LoginService._httpClient.GetAsync(getUrl);
                if (!getResponse.IsSuccessStatusCode)
                {
                    response.Message = $"No se pudo obtener el recuento {RequestData.DocEntry} desde SAP.";
                    log.Error($"[ERROR] No se pudo obtener recuento desde SAP | DocEntry: {RequestData.DocEntry} | StatusCode: {getResponse.StatusCode}");
                    return response;
                }

                var getResult = await getResponse.Content.ReadAsStringAsync();
                dynamic recuentoExistente = JsonConvert.DeserializeObject(getResult);

                log.Info($"[OK] Recuento obtenido correctamente desde SAP | DocEntry: {RequestData.DocEntry}");

                // ✅ Usar fecha original del documento
                DateTime sapDate = recuentoExistente.CountDate != null
                    ? DateTime.Parse(recuentoExistente.CountDate.ToString())
                    : DateTime.Now.Date;

                // 🔹 Construir PATCH
                dynamic recuentoPatch = new ExpandoObject();
                var dictRC = (IDictionary<string, object>)recuentoPatch;
                dictRC["CountDate"] = sapDate.ToString("yyyy-MM-dd");
                dictRC["Remarks"] = $"Total de escaneos incluidos en el recuento: {totalCantidadEscaneada}" + Environment.NewLine
                    + "🔒 Ojo favor de considerar que ya no se pueden cargar mas elementos a este folio";
                dictRC["InventoryCountingLines"] = new List<object>();

                // 🔹 Diccionario de líneas existentes
                var lineasExistentes = new Dictionary<string, int>();
                foreach (var linea in recuentoExistente.InventoryCountingLines)
                {
                    string key = $"{linea.ItemCode}|{linea.WarehouseCode}";
                    lineasExistentes[key] = (int)linea.LineNumber;
                }

                // 🔹 Agrupar artículos EXISTENTES por ItemCode + WarehouseCode
                var gruposExistentes = articulosExistentes
                    .GroupBy(a => new
                    {
                        ItemCode = a["CodigoArticulo"].ToString(),
                        WarehouseCode = a["CodigoAlmacen"].ToString()
                    });

                // 🔹 Agrupar artículos NUEVOS por ItemCode + WarehouseCode
                var gruposNuevos = articulosNuevos
                    .GroupBy(a => new
                    {
                        ItemCode = a["CodigoArticulo"].ToString(),
                        WarehouseCode = a["CodigoAlmacen"].ToString()
                    });


                log.Info($"[OK] Grupos de líneas existentes: {gruposExistentes.Count()} | Grupos de líneas nuevas: {gruposNuevos.Count()}");

                // ============================================================================
                // 🔹 PROCESAR LÍNEAS EXISTENTES (con LineNumber)
                // ============================================================================
                foreach (var grupo in gruposExistentes)
                {
                    string itemCode = grupo.Key.ItemCode;
                    string whsCode = grupo.Key.WarehouseCode;
                    string key = $"{itemCode}|{whsCode}";

                    var newLine = new ExpandoObject() as IDictionary<string, object>;
                    newLine["ItemCode"] = itemCode;
                    newLine["WarehouseCode"] = whsCode;
                    newLine["Counted"] = "tYES";

                    // 🔹 Buscar línea existente en SAP (DEBE existir porque ExisteEnRecuento = 'SI')
                    int lineNumber = lineasExistentes.ContainsKey(key) ? lineasExistentes[key] : -1;

                    if (lineNumber < 0)
                    {
                        log.Warn($"[WARN] Artículo marcado como existente pero no se encontró en SAP | ItemCode: {itemCode} | Warehouse: {whsCode}");
                        continue; // Saltar esta línea
                    }

                    // 🔹 Obtener lotes ya existentes en SAP
                    var existingBatches = new Dictionary<string, dynamic>();
                    JArray linesArray = recuentoExistente.InventoryCountingLines as JArray;
                    if (linesArray != null)
                    {
                        var existingLine = linesArray
                            .FirstOrDefault(l => (int)l["LineNumber"] == lineNumber);

                        if (existingLine != null && existingLine["InventoryCountingBatchNumbers"] != null)
                        {
                            foreach (var b in existingLine["InventoryCountingBatchNumbers"])
                            {
                                string batchNum = b["BatchNumber"].ToString();
                                existingBatches[batchNum] = new
                                {
                                    BatchNumber = batchNum,
                                    Quantity = (double)b["Quantity"],
                                    BaseLineNumber = (int)b["BaseLineNumber"]
                                };
                            }
                        }
                    }

                    double totalCantidad = 0;
                    var batchList = new List<object>();

                    foreach (var linea in grupo)
                    {
                        double cantidad = Convert.ToDouble(linea["Cantidad"]);
                        totalCantidad += cantidad;
                        string lote = linea["Lote"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(lote))
                        {
                            if (existingBatches.ContainsKey(lote))
                            {
                                var existing = existingBatches[lote];
                                batchList.Add(new
                                {
                                    existing.BatchNumber,
                                    Quantity = cantidad,
                                    existing.BaseLineNumber
                                });
                            }
                            else
                            {
                                batchList.Add(new
                                {
                                    BatchNumber = lote,
                                    Quantity = cantidad,
                                    BaseLineNumber = lineNumber
                                });
                            }
                        }
                    }

                    newLine["CountedQuantity"] = totalCantidad;
                    newLine["LineNumber"] = lineNumber; // ✅ Incluir LineNumber para actualizar

                    if (batchList.Count > 0)
                        newLine["InventoryCountingBatchNumbers"] = batchList;

                    ((List<object>)dictRC["InventoryCountingLines"]).Add(newLine);
                    log.Info($"[OK] Línea EXISTENTE procesada | ItemCode: {itemCode} | Warehouse: {whsCode} | LineNumber: {lineNumber} | Cantidad: {totalCantidad}");
                }

                // ============================================================================
                // 🔹 PROCESAR LÍNEAS NUEVAS (Agregar SIN contar primero)
                // ============================================================================
                foreach (var grupo in gruposNuevos)
                {
                    string itemCode = grupo.Key.ItemCode;
                    string whsCode = grupo.Key.WarehouseCode;

                    var newLine = new ExpandoObject() as IDictionary<string, object>;
                    newLine["ItemCode"] = itemCode;
                    newLine["WarehouseCode"] = whsCode;
                    newLine["Counted"] = "tNO"; // ✅ SIN contar en el primer PATCH

                    // ❌ NO incluir CountedQuantity
                    // ❌ NO incluir InventoryCountingBatchNumbers
                    // ❌ NO incluir LineNumber

                    ((List<object>)dictRC["InventoryCountingLines"]).Add(newLine);
                    log.Info($"[OK] Línea NUEVA agregada (sin contar) | ItemCode: {itemCode} | Warehouse: {whsCode}");
                }

                int totalLineasProcesadas = gruposExistentes.Count() + gruposNuevos.Count();
                log.Info($"[OK] PATCH preparado correctamente | DocEntry: {RequestData.DocEntry} | Total líneas: {totalLineasProcesadas} (Existentes: {gruposExistentes.Count()}, Nuevas: {gruposNuevos.Count()})");

                var url = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryCountings({RequestData.DocEntry})";
                var jsonBody = JsonConvert.SerializeObject(recuentoPatch);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
                var patchResponse = await LoginService._httpClient.SendAsync(patchRequest);
                var patchResult = await patchResponse.Content.ReadAsStringAsync();

                if (patchResponse.IsSuccessStatusCode)
                {
                    response.IsError = false;
                    response.Message = $"Recuento {RequestData.DocEntry} actualizado correctamente en SAP.";

                    log.Info($"[OK] Recuento actualizado correctamente en SAP | DocEntry: {RequestData.DocEntry}");

                    // ============================================================================
                    // 🔹 SI HAY ARTÍCULOS NUEVOS, HACER SEGUNDO PATCH CON LOTES
                    // ============================================================================
                    if (gruposNuevos.Count() > 0)
                    {
                        log.Info($"[OK] Iniciando segundo PATCH para actualizar artículos nuevos con lotes | Cantidad: {gruposNuevos.Count()}");

                        // 🔹 1. Volver a obtener el documento actualizado (ahora tiene los LineNumber de las líneas nuevas)
                        var getUrl2 = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryCountings({RequestData.DocEntry})";
                        var getResponse2 = await LoginService._httpClient.GetAsync(getUrl2);

                        if (!getResponse2.IsSuccessStatusCode)
                        {
                            log.Error($"[ERROR] No se pudo obtener recuento actualizado para segundo PATCH | DocEntry: {RequestData.DocEntry}");
                        }
                        else
                        {
                            var getResult2 = await getResponse2.Content.ReadAsStringAsync();
                            dynamic recuentoActualizado = JsonConvert.DeserializeObject(getResult2);

                            // 🔹 2. Crear diccionario de líneas actualizadas
                            var lineasActualizadas = new Dictionary<string, int>();
                            foreach (var linea in recuentoActualizado.InventoryCountingLines)
                            {
                                string key = $"{linea.ItemCode}|{linea.WarehouseCode}";
                                lineasActualizadas[key] = (int)linea.LineNumber;
                            }

                            // 🔹 3. Preparar segundo PATCH solo con artículos nuevos
                            dynamic segundoPatch = new ExpandoObject();
                            var dictRC2 = (IDictionary<string, object>)segundoPatch;
                            dictRC2["InventoryCountingLines"] = new List<object>();

                            foreach (var grupo in gruposNuevos)
                            {
                                string itemCode = grupo.Key.ItemCode;
                                string whsCode = grupo.Key.WarehouseCode;
                                string key = $"{itemCode}|{whsCode}";

                                // 🔹 Buscar el LineNumber asignado por SAP
                                if (!lineasActualizadas.ContainsKey(key))
                                {
                                    log.Warn($"[WARN] No se encontró LineNumber para artículo nuevo | ItemCode: {itemCode} | Warehouse: {whsCode}");
                                    continue;
                                }

                                int lineNumber = lineasActualizadas[key];

                                var newLine = new ExpandoObject() as IDictionary<string, object>;
                                newLine["LineNumber"] = lineNumber;
                                newLine["ItemCode"] = itemCode;
                                newLine["WarehouseCode"] = whsCode;
                                newLine["Counted"] = "tYES"; // ✅ Ahora SÍ marcar como contado

                                double totalCantidad = 0;
                                var batchList = new List<object>();

                                foreach (var linea in grupo)
                                {
                                    double cantidad = Convert.ToDouble(linea["Cantidad"]);
                                    totalCantidad += cantidad;
                                    string lote = linea["Lote"]?.ToString();

                                    if (!string.IsNullOrWhiteSpace(lote))
                                    {
                                        batchList.Add(new
                                        {
                                            BatchNumber = lote,
                                            Quantity = cantidad,
                                            BaseLineNumber = lineNumber // ✅ Ahora SÍ tenemos el LineNumber
                                        });
                                    }
                                }

                                newLine["CountedQuantity"] = totalCantidad;

                                if (batchList.Count > 0)
                                    newLine["InventoryCountingBatchNumbers"] = batchList;

                                ((List<object>)dictRC2["InventoryCountingLines"]).Add(newLine);
                                log.Info($"[OK] Artículo nuevo preparado para segundo PATCH | ItemCode: {itemCode} | LineNumber: {lineNumber} | Cantidad: {totalCantidad} | Lotes: {batchList.Count}");
                            }

                            // 🔹 4. Ejecutar segundo PATCH
                            var url2 = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryCountings({RequestData.DocEntry})";
                            var jsonBody2 = JsonConvert.SerializeObject(segundoPatch);
                            var content2 = new StringContent(jsonBody2, Encoding.UTF8, "application/json");

                            var patchRequest2 = new HttpRequestMessage(new HttpMethod("PATCH"), url2) { Content = content2 };
                            var patchResponse2 = await LoginService._httpClient.SendAsync(patchRequest2);
                            var patchResult2 = await patchResponse2.Content.ReadAsStringAsync();

                            if (patchResponse2.IsSuccessStatusCode)
                            {
                                log.Info($"[OK] Segundo PATCH exitoso - Artículos nuevos actualizados con lotes | DocEntry: {RequestData.DocEntry}");
                                response.Message = $"Recuento {RequestData.DocEntry} actualizado correctamente (con artículos nuevos y lotes).";
                            }
                            else
                            {
                                log.Error($"[ERROR] Error en segundo PATCH | DocEntry: {RequestData.DocEntry} | Response: {patchResult2}");
                                response.Message += $" ADVERTENCIA: Los artículos nuevos se agregaron pero no se pudieron actualizar con lotes.";
                            }
                        }
                    }

                    var updateParams = new Dictionary<string, string>
                        {
                            { "Folio", RequestData.FolioRI },
                            { "RecuentoInventario", response.Message }
                        };

                    GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosRecuentosHHRI, updateParams);
                    log.Info($"[OK] Documento actualizado en tabla local | FolioRI: {RequestData.FolioRI}");
                }
                else
                {

                    string decodedMessage = patchResult;

                    try
                    {
                        var parsed = JsonConvert.DeserializeObject(patchResult);
                        decodedMessage = JsonConvert.SerializeObject(parsed, Formatting.Indented);
                    }
                    catch
                    {
                        decodedMessage = patchResult;
                    }

                    log.Error($"[ERROR] Error al ejecutar PATCH en SAP | DocEntry: {RequestData.DocEntry} | Response: {decodedMessage}");

                    try
                    {
                        dynamic errorObj = JsonConvert.DeserializeObject(patchResult);
                        int errorCode = errorObj.error.code;
                        string errorMessage = errorObj.error.message.value;

                        if (errorMessage.Contains("No es posible añadir una línea"))
                        {
                            errorMessage = "SAP no permite enviar nuevos lotes de una linea que ha sido contada anteriormente con al menos un lote," +
                                " para poder actualizar el recuento es necesario quitar los lotes contados del documento, marcar la cantidad en 0 y desmarcar el indicador *Contado* " +
                                " y actualizar en SAP.";
                            response.Message = $"{errorMessage}";
                        }
                        else
                            response.Message = $"{errorCode} {errorMessage}";
                    }
                    catch
                    {
                        response.Message = $"{patchResult}";
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en UpdateInventoryCountingAsync | Usuario: {RequestData?.Usuario} | DocEntry: {RequestData?.DocEntry} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                response.Message = $"Excepción: {ex.Message}";
            }

            return response;
        }


        #endregion

        #region General
        //Funcion para generar el codigo de barras
        private string GenerarLote(string ItemCode, string Usuario, string Folio)
        {
            try
            {
                Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
                bool LoteValidado = false;
                string CodigoBarras = string.Empty;
                int intentos = 0;
                var PlantillaUsuario = new Dictionary<string, string>
                {
                    { "Usuario", Usuario }
                };

                while (LoteValidado == false)
                {
                    string PlantillaEMD = GlobalCommands.ExecuteProcedure(AD.GCGetPlantillaEtiquetaHHEMD, PlantillaUsuario);
                    JArray PlantillasData = JArray.Parse(PlantillaEMD);
                    string PlantillaSeleccionada = (string)PlantillasData[0]["Plantilla"];


                    var PlantillaParametros = new Dictionary<string, string>
                {
                    { "Plantilla", PlantillaSeleccionada },
                    { "ItemCode", ItemCode }
                };

                    string DatosCB = GlobalCommands.ExecuteProcedure(AD.GCGetNomenclaturaEtiqueta, PlantillaParametros);
                    JArray BarCodeData = JArray.Parse(DatosCB);

                    //Ruta del archivo JSON
                    string filePath = string.Empty;
                    switch (BarCodeData[0]["U_IdFecha"].ToString())
                    {
                        case "Semanal":
                            filePath = ConfigurationManager.AppSettings["ConsecutivosEDSemanal"];
                            break;
                        case "Mensual":
                            filePath = ConfigurationManager.AppSettings["ConsecutivosEDMensual"];
                            break;
                        case "Semestral":
                            filePath = ConfigurationManager.AppSettings["ConsecutivosEDSemestral"];
                            break;
                        case "Anual":
                            filePath = ConfigurationManager.AppSettings["ConsecutivosEDAnuales"];
                            break;
                    }


                    // Leer los consecutivos desde el archivo JSON
                    var consecutivos = LeerConsecutivos(filePath);

                    ConsecutivoInfoED NuevoConsecutivo = consecutivos.EntradasDirectas;

                    DateTime ahora = DateTime.Now;

                    // Reinicio por periodicidad
                    if (RequiereReinicio(NuevoConsecutivo.UltimaFechaReinicio, ahora, BarCodeData[0]["U_IdFecha"].ToString()))
                    {
                        NuevoConsecutivo.Consecutivo = 1;
                        NuevoConsecutivo.UltimaFechaReinicio = ahora;
                    }
                    else
                    {
                        NuevoConsecutivo.Consecutivo++;
                    }

                    // Actualiza JSON
                    consecutivos.EntradasDirectas.UltimaFechaReinicio = ahora;
                    consecutivos.EntradasDirectas.Consecutivo = NuevoConsecutivo.Consecutivo;

                    GuardarConsecutivos(filePath, consecutivos);


                    // 🔥 FORMATEAR EL CONSECUTIVO A 4 DIGITOS
                    string consecutivoFormateado = NuevoConsecutivo.Consecutivo.ToString("D4");

                    if (BarCodeData.Count > 0)
                    {
                        CodigoBarras =
                            (string)BarCodeData[0]["U_IdPeriodo"] +
                            (string)BarCodeData[0]["Fecha"] +
                            (string)BarCodeData[0]["U_IdUnico"] +
                            //(string)BarCodeData[0]["Linea"] +
                            (string)BarCodeData[0]["U_IdProducto"] + "_" +
                            consecutivoFormateado;

                        //Antes de retornar validar si ese lote ya existe
                        RequestParameters.Clear();
                        RequestParameters.Add("ItemCode", ItemCode);
                        RequestParameters.Add("Lote", CodigoBarras);
                        RequestParameters.Add("Folio", Folio);

                        string result = GlobalCommands.ExecuteProcedure(AD.GCValidarLoteInventariosHH, RequestParameters);
                        result = result.Replace("?", "●");
                        JArray LoteResult = JArray.Parse(result);

                        if (LoteResult[0]["Codigo"].ToString().Contains("LOTE_REGISTRADO"))
                        {
                            LoteValidado = false;
                            intentos++;
                        }
                        else
                            LoteValidado = true;
                    }
                    else
                    {
                        CodigoBarras = "SIN_CONFIGURACION_" + consecutivoFormateado;
                        LoteValidado = true;
                    }
                }
                //Si el lote ya existia en SAP cuando se genero dinamicamente, se marca como recalculado debido a que se tuvo que incrementar de nuevo el consecutivo
                if (intentos > 0)
                    CodigoBarras += "/recalculado";

                return CodigoBarras;
            }
            catch (Exception E)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append("Error:");
                Error.Append(E.Message != null ? E.Message.ToString() : string.Empty);
                Error.Append(E.InnerException != null ? E.InnerException.ToString() : string.Empty);
                return Error.ToString();
            }
        }

        //Funcion para generar el codigo de barras
        private string GenerarLoteMP(string producto, string tipoEtiqueta, string FechaEntrada)
        {
            try
            {
                //Ruta del archivo JSON
                string filePath = ConfigurationManager.AppSettings["ConsecutivosEntradasDirectasMP"];
                //Número consecutivo
                ConsecutivoInfoMP NuevoConsecutivo = new ConsecutivoInfoMP();
                // Leer los consecutivos desde el archivo JSON
                var consecutivos = LeerConsecutivosMP(filePath);

                if (tipoEtiqueta.ToLower() == "tubos")
                {
                    NuevoConsecutivo = consecutivos.Tubos;
                }
                else if (tipoEtiqueta.ToLower() == "pacas")
                {
                    NuevoConsecutivo = consecutivos.Pacas;
                }
                //Antes de leer el consecutivo validar si ya paso un dia
                DateTime ahora = DateTime.Now;

                if (RequiereReinicio(NuevoConsecutivo.UltimaFechaReinicio, ahora))
                {
                    NuevoConsecutivo.Consecutivo = 1; // Reiniciar el contador
                    NuevoConsecutivo.UltimaFechaReinicio = ahora; // Actualizar la fecha de reinicio
                }

                else
                {
                    // Incrementar el consecutivo
                    NuevoConsecutivo.Consecutivo++;
                }

                if (tipoEtiqueta.ToLower() == "tubos")
                {
                    consecutivos.Tubos.UltimaFechaReinicio = ahora;
                    consecutivos.Tubos.Consecutivo = NuevoConsecutivo.Consecutivo;
                }
                else if (tipoEtiqueta.ToLower() == "pacas")
                {
                    consecutivos.Pacas.UltimaFechaReinicio = ahora;
                    consecutivos.Pacas.Consecutivo = NuevoConsecutivo.Consecutivo;
                }

                // Guardar los cambios en el archivo JSON
                GuardarConsecutivos(filePath, consecutivos);

                // Dividir el producto en partes usando espacios como separador
                string[] partes = producto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Verificar que haya al menos una parte
                if (partes.Length == 0)
                    throw new ArgumentException("El producto no tiene un formato válido");

                // Tomar la primera parte (ejemplo: FSX76)
                string codigoBarras = partes[0];

                // Si hay más partes, tomar las dos primeras letras de la segunda y última parte
                if (partes.Length > 1)
                {
                    // Segunda parte (ejemplo: RELIE)
                    if (partes[1].Length >= 2)
                        codigoBarras += partes[1].Substring(0, 2); // Tomar las dos primeras letras (RE)

                    // Última parte (ejemplo: TEST)
                    if (partes.Length > 2 && partes[partes.Length - 1].Length >= 2)
                        codigoBarras += partes[partes.Length - 1].Substring(0, 2); // Tomar las dos primeras letras (TE)
                }

                // Añadir la fecha formateada al código de barras
                codigoBarras += FechaEntrada;
                // Añadir el separador y el consecutivo
                codigoBarras += "_" + NuevoConsecutivo.Consecutivo.ToString("D4");


                return codigoBarras;
            }
            catch (Exception E)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append("Error:");
                Error.Append(E.Message != null ? E.Message.ToString() : string.Empty);
                Error.Append(E.InnerException != null ? E.InnerException.ToString() : string.Empty);
                return Error.ToString();
            }
        }

        // Función para verificar si ha pasado un domingo desde la última fecha de reinicio del consecutivo
        private bool RequiereReinicio(DateTime ultimaFechaReinicio, DateTime ahora)
        {
            // Si no hay una fecha de reinicio previa, no ha pasado un domingo
            if (ultimaFechaReinicio == DateTime.MinValue)
                return false;

            if (ahora.Date > ultimaFechaReinicio.Date)
                return true;

            return false;
        }
        private bool RequiereReinicio(DateTime ultimaFechaReinicio, DateTime ahora, string periodo)
        {
            if (ultimaFechaReinicio == DateTime.MinValue)
                return false;

            if (string.IsNullOrWhiteSpace(periodo))
                return false;

            periodo = periodo.Trim().ToUpperInvariant();

            switch (periodo)
            {
                case "SEMANAL":
                    // 7 días completos
                    return ahora.Date >= ultimaFechaReinicio.Date.AddDays(7);

                case "MENSUAL":
                    // 1 mes calendario
                    return ahora.Date >= ultimaFechaReinicio.Date.AddMonths(1);

                case "SEMESTRAL":
                    // 6 meses calendario
                    return ahora.Date >= ultimaFechaReinicio.Date.AddMonths(6);

                case "ANUAL":
                    // 1 año calendario
                    return ahora.Date >= ultimaFechaReinicio.Date.AddYears(1);

                default:
                    return false;
            }
        }

        //Funcion para leer en JSON
        private ConsecutivosEntradas LeerConsecutivos(string filePath)
        {
            if (!File.Exists(filePath))
            {
                // Si el archivo no existe, crea uno nuevo con valores iniciales
                var consecutivos = new ConsecutivosEntradas
                {
                    EntradasDirectas = new ConsecutivoInfoED { Consecutivo = 0, UltimaFechaReinicio = DateTime.Now },
                };
                File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
                return consecutivos;
            }

            // Leer el archivo JSON
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ConsecutivosEntradas>(json);
        }
        private ConsecutivosMateriaPrima LeerConsecutivosMP(string filePath)
        {
            if (!File.Exists(filePath))
            {
                // Si el archivo no existe, crea uno nuevo con valores iniciales
                var consecutivos = new ConsecutivosMateriaPrima
                {
                    Tubos = new ConsecutivoInfoMP { Consecutivo = 0, UltimaFechaReinicio = DateTime.Now },
                    Pacas = new ConsecutivoInfoMP { Consecutivo = 0, UltimaFechaReinicio = DateTime.Now }
                };
                File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
                return consecutivos;
            }

            // Leer el archivo JSON
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ConsecutivosMateriaPrima>(json);
        }
        //Funcion para Escribir en JSON
        private void GuardarConsecutivos(string filePath, ConsecutivosEntradas consecutivos)
        {
            // Guardar el archivo JSON
            File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
        }
        //Funcion para Escribir en JSON
        private void GuardarConsecutivos(string filePath, ConsecutivosMateriaPrima consecutivos)
        {
            // Guardar el archivo JSON
            File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
        }
        //Leer plantilla
        private Dictionary<string, string> ImprimirPlantilla(string Usuario)
        {
            try
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("Usuario", Usuario);

                string PlantillaEMD = GlobalCommands.ExecuteProcedure(AD.GCGetPlantillaEtiquetaHHEMD, parameters); //Obtener la plantilla seleccionada desde DB
                JArray PlantillasData = JArray.Parse(PlantillaEMD);

                // Ruta donde están los archivos de plantillas
                string rutaPlantillas = ConfigurationManager.AppSettings["PlantillasRuta"];

                if (PlantillasData.Count > 0)
                {
                    string PlantillaSeleccionada = (string)PlantillasData[0]["Plantilla"];


                    // Usa la ruta del config, más segura
                    string ruta = Path.Combine(rutaPlantillas, $"{PlantillaSeleccionada}.txt");

                    if (File.Exists(ruta))
                    {
                        parameters.Clear();
                        parameters.Add("PLANTILLA", PlantillaSeleccionada);
                        parameters.Add("ZPL", File.Exists(ruta) ? File.ReadAllText(ruta) : $"El archivo '{ruta}' no existe.");
                    }
                }
                else
                {
                    parameters.Clear();
                    parameters.Add("PLANTILLA", string.Empty);
                    parameters.Add("ZPL", "SIN PLANTILLA SELECCIONADA");
                }

                return parameters;
            }
            catch (JsonException jex)
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("ZPL", $"Error al procesar la plantilla desde DB: {jex.Message}");
                // Error al parsear JSON
                return parameters;
            }
            catch (IOException ioex)
            {
                // Errores de acceso al archivo
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("ZPL", $"Error de lectura del archivo: {ioex.Message}");
                // Error al parsear JSON
                return parameters;
            }
            catch (Exception ex)
            {
                // Cualquier otro error inesperado
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("ZPL", $"Error inesperado: {ex.Message}");
                return parameters;
            }
        }

        private string GetNumeroLote(string lote)
        {
            if (string.IsNullOrWhiteSpace(lote))
                return string.Empty;

            if (!lote.Contains("_"))
                return string.Empty;

            string[] partes = lote.Split('_');
            return partes.Last(); // Devuelve lo que esté después del último "_"
        }

        public string EnviarZPLConValidacion(string printer, string zpl)
        {
            try
            {
                using (TcpClient client = new TcpClient(printer, 6101))
                {
                    client.ReceiveTimeout = 10000;
                    client.SendTimeout = 10000;
                    NetworkStream stream = client.GetStream();

                    // Comando ~HS (Host Status) - funciona perfectamente
                    byte[] statusCmd = Encoding.ASCII.GetBytes("~HS\n");
                    stream.Write(statusCmd, 0, statusCmd.Length);
                    Thread.Sleep(800);

                    if (stream.DataAvailable)
                    {
                        byte[] buffer = new byte[2048];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        // Limpiar caracteres de control
                        response = response
                            .Replace("\u0002", "")
                            .Replace("\u0003", "")
                            .Trim();

                        // Dividir en líneas
                        string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length >= 2)
                        {
                            // Analizar la segunda línea (la más importante)
                            // Formato: 001,0,0,0,1,2,6,0,00000000,1,000
                            string[] status = lines[1].Split(',');

                            if (status.Length >= 6)
                            {
                                List<string> errores = new List<string>();

                                // status[0]: Estado de comunicación
                                if (status[0] == "000")
                                    errores.Add("Problema de comunicación con la impresora");

                                // status[4]: Pause - SOLO si NO es por cabezal abierto
                                if (status[4] == "2" && status[5] != "2")
                                {
                                    errores.Add("Impresora pausada por error");
                                }
                                else if (status[4] == "2")
                                {
                                    errores.Add("Impresora pausada por error");
                                }
                                // Si hay errores, devolverlos
                                if (errores.Count > 0)
                                {
                                    return "ERROR: " + string.Join(", ", errores);
                                }

                                // status[2]: Paper Out (sin papel)
                                //if (status[2] == "1")
                                //    errores.Add("Sin papel");

                                // status[3]: Ribbon Out (sin cinta)
                                //if (status[3] == "1")
                                //    errores.Add("Sin cinta (ribbon)");

                                // status[5]: Head (estado del cabezal) - VERIFICAR PRIMERO
                                //if (status[5] == "2")
                                //{
                                //    errores.Add("Cabezal abierto");
                                //}
                                // status[5]: Head (estado del cabezal)
                                //if (status[5] == "1")
                                //{
                                //    errores.Add("Cabezal calentándose");
                                //}
                            }
                            else
                            {
                                return "ERROR: Respuesta de estado incompleta";
                            }
                        }
                        else
                        {
                            return "ERROR: Respuesta de estado en formato no esperado";
                        }
                    }
                    else
                    {
                        return "ERROR: No se recibió respuesta de la impresora";
                    }

                    // Si llegamos aquí, todo está bien - enviar ZPL
                    byte[] zplBytes = Encoding.ASCII.GetBytes(zpl);
                    stream.Write(zplBytes, 0, zplBytes.Length);

                    return "OK"; // Impresión enviada correctamente
                }
            }
            catch (SocketException ex)
            {
                return $"ERROR: No se pudo conectar a la impresora {printer} - {ex.Message}";
            }
            catch (IOException ex)
            {
                return $"ERROR: Problema de comunicación - {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        #endregion
    }
}