using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Xml.Serialization;

namespace RESTAPIFFISA.Controllers
{
    public class VentasMovilController : Controller
    {
        readonly LogicaVentasMovil Logic = new LogicaVentasMovil();
        private static readonly ILog log = LogManager.GetLogger("VentasMovil");


        [HttpPost]
        public async Task<string> GenerarEntregaMercanciaOV()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosVentas.EntregasMercanciaVentasHHEMOV RequestData = new AccesoDatosVentas.EntregasMercanciaVentasHHEMOV();
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.EntregasMercanciaVentasHHEMOV));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosVentas.EntregasMercanciaVentasHHEMOV)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("=======================ENTREGA MERCANCÍA=======================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta generar la entrega de mercancía para el documento de entregas: {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador}");

                string result = string.Empty;
                // Asegurarse de que se haya iniciado sesión
                GlobalCommands.SapResponse oLoggedIn = null;
                GlobalCommands.SapResponse oEntregaMaercancia = null;

                log.Info($"[INFO] Iniciando sesión en SAP Service Layer para usuario: {RequestData.Usuario} con folio de entrega: {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador}");

                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    log.Error($"[ERROR] No se pudo autenticar en SAP Service Layer para generar la entrega del folio: {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador} | Error: {oLoggedIn.Message}");

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = $"No fue posible generar la entrega de mercancía: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    log.Info($"[OK] Autenticación exitosa en SAP Service Layer {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador}");

                    if (RequestData.IsBorrador == "SI")
                    {
                        log.Info($"[INFO] Generando documento borrador para entrega | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador}");
                        oEntregaMaercancia = await Logic.CreateDeliveryDraftAutomaticFromOrderAsync(RequestData);
                    }
                    else
                    {
                        log.Info($"[INFO] Generando entrega definitiva | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario} | OV DocEntry: {RequestData.OrdenVenta} | IsBorrador: {RequestData.IsBorrador}");
                        oEntregaMaercancia = await Logic.CreateDeliveryFromOrderAutomaticAsync(RequestData);
                    }

                    if (oEntregaMaercancia.IsError)
                    {
                        parameters.Clear();

                        string CustomMessage = (RequestData.IsBorrador == "SI" ? "el documento preeliminar para entrega" : "la entrega");

                        log.Error($"[ERROR] Error al generar {CustomMessage} | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta} | Usuario: {RequestData.Usuario} | Error: {oEntregaMaercancia.Message}");

                        //Manejar el error persistente
                        if (oEntregaMaercancia.Message.Contains("Cantidad insuficiente") || oEntregaMaercancia.Message.Contains("La cantidad recae en un inventario negativo"))
                        {
                            log.Warn($"[WARN] Error de inventario detectado, validando si la entrega ya existe | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");

                            // ✅ Validar si ya existe
                            string entregaExistente = Logic.ValidarEntregaExistente(RequestData.FolioEM, RequestData.Usuario);

                            if (!string.IsNullOrEmpty(entregaExistente))
                            {
                                // ✅ La entrega ya existe
                                log.Info($"[OK] Entrega existente encontrada por error persistente | DocNum: {entregaExistente} | FolioEM: {RequestData.FolioEM} | Usuario: {RequestData.Usuario}");

                                oEntregaMaercancia.IsError = false;
                                oEntregaMaercancia.Message = $"Entrega creada exitosamente. Documento SAP: {entregaExistente}" +
                                                     Environment.NewLine + Environment.NewLine +
                                                     "● Cantidad de rollos comprendidos en la entrega: " + oEntregaMaercancia.RollosComprendidos +
                                                     Environment.NewLine + Environment.NewLine;

                                oEntregaMaercancia.OrdenVenta = oEntregaMaercancia.OrdenVenta;

                                log.Info($"[INFO] Actualizando documento de entrega existente | DocNum: {entregaExistente} | FolioEM: {RequestData.FolioEM}");

                                // -----------------------------
                                // ACTUALIZAR DOCUMENTO
                                // -----------------------------
                                Logic.UpdateDocumentDelivery(RequestData, entregaExistente);

                                log.Info($"[OK] Documento actualizado exitosamente | DocNum: {entregaExistente} | FolioEM: {RequestData.FolioEM}");

                                //Retornar como documento normal
                                jsonResponse = new AccesoDatos.JsonResponse()
                                {
                                    Status = "SI",
                                    Message = oEntregaMaercancia.Message,
                                    Data = new List<Dictionary<string, object>>()
                                };
                            }
                            else
                            {
                                log.Error($"[ERROR] No se encontró entrega existente y no se pudo crear nueva | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta}");

                                jsonResponse = new AccesoDatos.JsonResponse()
                                {
                                    Status = "NO",
                                    Message = $"No fue posible generar {CustomMessage} de mercancía para el documento: {oEntregaMaercancia.OrdenVenta} {oEntregaMaercancia.Message}",
                                    Data = new List<Dictionary<string, object>>()
                                };
                            }
                        }
                        else
                        {
                            log.Error($"[Error] No fue posible generar {CustomMessage} de mercancía para el documento: {oEntregaMaercancia.OrdenVenta} {oEntregaMaercancia.Message}");

                            jsonResponse = new AccesoDatos.JsonResponse()
                            {
                                Status = "NO",
                                Message = $"No fue posible generar {CustomMessage} de mercancía para el documento: {oEntregaMaercancia.OrdenVenta} {oEntregaMaercancia.Message}",
                                Data = new List<Dictionary<string, object>>()
                            };
                        }
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oEntregaMaercancia.Message,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }

                log.Info($"[INFO] Cerrando sesión en SAP Service Layer | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta} | Usuario: {RequestData.Usuario} | IsBorrador: {RequestData.IsBorrador}");

                //Cerrar sesion
                await Logic.LoginService.LogoutAsyncHttpClient();

                log.Info($"[OK] Proceso completado para usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | Status: {jsonResponse.Status}");

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en GenerarEntregaMercanciaOV,No es posible generar la entrega de mercancía | Usuario: {RequestData.Usuario} | FolioEM: {RequestData.FolioEM} | OV: {RequestData.OrdenVenta} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString();
                _ = RequestData.IsBorrador == "SI" ? "el documento preeliminar para entrega" : "la entrega";
                string msg = "No es posible generar la entrega de mercancía: " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
                string finalmessage = Logic.GlobalCommands.Excepcion(ex, msg).ToString();
                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = finalmessage,
                    Data = new List<Dictionary<string, object>>()
                };
                string result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
        }
        [HttpPost]
        public async Task<string> CancelarEntregaMercancia()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosVentas.CancelacionEntregaRequest RequestData = new AccesoDatosVentas.CancelacionEntregaRequest();

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.CancelacionEntregaRequest));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados
                        RequestData = (AccesoDatosVentas.CancelacionEntregaRequest)serializer.Deserialize(stringReader);
                    }
                }

                string result = string.Empty;

                // Asegurarse de que se haya iniciado sesión
                GlobalCommands.SapResponse oLoggedIn = null;
                GlobalCommands.SapResponse oCancelacionEntrega = null;

                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = $"No fue posible cancelar la entrega de mercancía: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // Llamar al método de cancelación en LogicaVentasMovil
                    oCancelacionEntrega = await Logic.CancelarEntregaMercanciaAsync(RequestData.DocEntry);

                    if (oCancelacionEntrega.IsError)
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "NO",
                            Message = $"No fue posible cancelar la entrega de mercancía (DocEntry: {RequestData.DocEntry}): {oCancelacionEntrega.Message}",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oCancelacionEntrega.Message,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }

                //Cerrar sesion
                await Logic.LoginService.LogoutAsyncHttpClient();

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString();
                string msg = "No es posible cancelar la entrega de mercancía: " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
                string finalmessage = Logic.GlobalCommands.Excepcion(ex, msg).ToString();

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = finalmessage,
                    Data = new List<Dictionary<string, object>>()
                };

                string result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
        }
        //Inserta el encabezado de la orden de venta para generar la Entrega de mercancia
        [HttpPost]
        public string InsertaEncabezadoVentasHHEMOV()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosVentas.EncabezadoVentasHHEMOV RequestData;
            string FolioReutilizable = string.Empty;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.EncabezadoVentasHHEMOV));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosVentas.EncabezadoVentasHHEMOV)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("=======================ENCABEZADO DOCUMENTO ENTREGAS===================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta generar nuevo documento de entregas de mercancía para la OV: {RequestData.SapDocument}");

                //VALIDAR SI HAY ALGUN FOLIO DEL MISMO SAP DOCUMENT VACIO Y PENDIENTE
                Dictionary<string, string> parametros = new Dictionary<string, string>
                {
                    { "SapDocument", RequestData.SapDocument },
                    { "Usuario", RequestData.Usuario }
                };

                string FolioPendiente = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarFolioEntregaPendienteHHEMOV, parametros);

                if (string.IsNullOrEmpty(FolioPendiente) || FolioPendiente.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible generar el documento base para la entrega de mercancía, no fue posible realizar el registro, intenta de nuevo más tarde.",
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] No fue posible generar el documento base para la entrega de mercancía del usuario: {RequestData.Usuario} | OV: {RequestData.SapDocument} | Error: No se pudo validar si hay un folio pendiente | Respuesta: {FolioPendiente}");

                    return null;
                }

                JArray FReutilizable = JArray.Parse(FolioPendiente);

                if (FReutilizable.Count > 0)
                {
                    FolioReutilizable = FReutilizable[0]["Folio"]?.ToString();

                    // Crear un objeto anónimo y serializarlo
                    var FRD = new[] { new { Folio = FolioReutilizable } };
                    string jsonFolio = JsonConvert.SerializeObject(FRD);

                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonFolio);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };

                    // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                    string FR = Logic.GlobalCommands.SerializeToXml(jsonResponse);

                    log.Info($"[OK] Se encontró un folio reutilizable para la solicitud del usuario: {RequestData.Usuario} | OV: {RequestData.SapDocument} | Folio: {FolioReutilizable}");
                    return FR;
                }

                parameters.Add("OrdenVenta", RequestData.OrdenVenta);
                parameters.Add("SapDocument", RequestData.SapDocument);
                parameters.Add("Cliente", RequestData.Cliente);
                parameters.Add("Usuario", RequestData.Usuario);
                parameters.Add("Comentarios", RequestData.Comentarios);
                parameters.Add("Estatus", RequestData.Estatus);


                //Generar folio nuevo para EM
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaEncabezadoVentasHHEMOV, parameters);

                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible generar el documento base para la entrega de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] Error al generar documento base para entrega de mercancía | Usuario: {RequestData.Usuario} | OrdenVenta: {RequestData.OrdenVenta} | SapDocument: {RequestData.SapDocument} | Error: {folioEM}");
                }
                else
                {
                    log.Info($"[OK] Folio EM generado exitosamente | Usuario: {RequestData.Usuario} | OrdenVenta: {RequestData.OrdenVenta} | SapDocument: {RequestData.SapDocument} | Cliente: {RequestData.Cliente} | Comentarios: {RequestData.Comentarios} | Folio: {folioEM}");

                    //Enviar notificacion a facturacion
                    bool notificacion = false;
                    parameters.Clear();
                    parameters.Add("DocEntry", RequestData.OrdenVenta);

                    //log.Info($"🔍 Validando consideraciones de la OV: {RequestData.SapDocument} para usuario: {RequestData.Usuario}");

                    string ValidacionesOV = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarOrdenVentaHHEMOV, parameters);
                    JArray MensajeData = JArray.Parse(ValidacionesOV);
                    string Consideraciones = MensajeData[0]["Consideraciones"].ToString();
                    Consideraciones = Consideraciones.Replace("?", "●");

                    if (!Consideraciones.Contains("OK")) //Si no esta todo en orden, enviar notificacion
                    {
                        //log.Warn($"⚠️ Se encontraron consideraciones para la OV: {RequestData.SapDocument} | Usuario: {RequestData.Usuario} | Consideraciones: {Consideraciones}");

                        notificacion = Logic.NotificacionFacturacion($"Notificación de consideraciones para la orden de venta {RequestData.SapDocument}",
                            $"Notificación de consideraciones para la orden de venta {RequestData.SapDocument} antes de facturar",
                            $"📝 Hola, se deben tomar en cuenta las siguientes consideraciones para el llenado de la información faltante de la orden de venta {RequestData.SapDocument} antes de facturar.",
                            Consideraciones,
                            RequestData.SapDocument, "Ventas");

                        if (notificacion)
                        {
                            log.Info($"[OK] Notificación de consideraciones enviada exitosamente para OV: {RequestData.SapDocument}");
                        }
                        else
                        {
                            log.Error($"[ERROR] Error al enviar notificación de consideraciones para OV: {RequestData.SapDocument} | Usuario: {RequestData.Usuario}");
                        }
                    }
                    else
                    {
                        log.Info($"[OK] La OV: {RequestData.SapDocument} cumple con todas las consideraciones, no requiere notificación");
                        notificacion = true;
                    }

                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = (notificacion == true ? "SI" : "EMAIL_MISSING"),
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Respuesta generada exitosamente | Status: {jsonResponse.Status} | Usuario: {RequestData.Usuario} | OV: {RequestData.SapDocument}");
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en InsertaEncabezadoVentasHHEMOV, No fue posible generar el documento para la entrega de mercancía | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible generar el documento para la entrega de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta las lineas de la orden de venta para generar la Entrega de mercancia
        [HttpPost]
        public async Task<string> InsertaLineasVentasHHEMOV()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosVentas.LineasVentasHHEMOV RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.LineasVentasHHEMOV));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosVentas.LineasVentasHHEMOV)serializer.Deserialize(stringReader);
                    }
                }

                parameters.Add("Folio", RequestData.Folio);
                parameters.Add("Articulo", RequestData.Articulo);
                parameters.Add("Linea", RequestData.Linea);
                parameters.Add("Almacen", RequestData.Almacen);
                parameters.Add("UMV", RequestData.UMV);
                parameters.Add("UMI", RequestData.UMI);
                parameters.Add("UMS", RequestData.UMS);
                parameters.Add("Lote", RequestData.Lote);
                parameters.Add("Cantidad", RequestData.Cantidad);
                parameters.Add("CantidadConversion", RequestData.CantidadConversion);
                parameters.Add("Referencia", RequestData.Referencia);
                parameters.Add("CantidadReferencia", RequestData.CantidadReferencia);

                log.Info("===================================================================================");
                log.Info("=======================LINEAS DOCUMENTO ENTREGAS===================================");
                log.Info("===================================================================================");
                log.Info($"[OK] El usuario {RequestData.Usuario} intenta ingresar un nuevo rollo para el folio de entregas: {RequestData.Folio} | Articulo: {RequestData.Articulo} | Linea: {RequestData.Linea} | Almacen: {RequestData.Almacen} | Lote: {RequestData.Lote} | Cantidad: {RequestData.Cantidad}");

                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaLineasVentasHHEMOV, parameters);

                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    log.Error($"[ERROR] No fue posible insertar lineas en el documento base para la entrega de mercancía | Folio: {RequestData.Folio} | Articulo: {RequestData.Articulo} | Error: {folioEM}");

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible insertar lineas en el documento base para la entrega de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    if (folioEM.Contains("Duplicado"))
                        log.Info($"[INFO] Se encontró un registro con el mismo lote, por lo cual ya no se registro");

                    else
                        log.Info($"[OK] Línea insertada exitosamente | Folio: {RequestData.Folio} | " +
                                 $"Articulo: {RequestData.Articulo} | Linea: {RequestData.Linea} | " +
                                 $"Almacen: {RequestData.Almacen} | UMV: {RequestData.UMV} | " +
                                 $"UMI: {RequestData.UMI} | UMS: {RequestData.UMS} | " +
                                 $"Lote: {RequestData.Lote} | Cantidad: {RequestData.Cantidad} | " +
                                 $"CantidadConversion: {RequestData.CantidadConversion} | " +
                                 $"Referencia: {RequestData.Referencia} | " +
                                 $"CantidadReferencia: {RequestData.CantidadReferencia}");

                    //Notificar a WEB sobre la cantidad de rollos cargados
                    try
                    {
                        //Generar folio nuevo para OC
                        parameters.Clear();
                        parameters.Add("Folio", RequestData.Folio);

                        log.Info($"[INFO] Obteniendo total de rollos para el folio: {RequestData.Folio}");

                        string Rollos = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetRollosHHEMOV, parameters);
                        JArray NumRollos = JArray.Parse(Rollos);
                        string TotalRollos = (string)NumRollos[0]["NumeroRollos"];
                        string OrdenVenta = (string)NumRollos[0]["OrdenVenta"];

                        log.Info($"[INFO] Notificando a WEB sobre actualización de rollos | OrdenVenta: {OrdenVenta} | TotalRollos: {TotalRollos}");

                        using (var client = new HttpClient())
                        {
                            client.Timeout = new TimeSpan(0, 0, 4);
                            //URL de la API
                            string url = ConfigurationManager.AppSettings["EndPointActualizacionRollos"];

                            //Datos a enviar
                            var json = "{ \"pedido\": \"" + OrdenVenta + "\", \"rollos\":" + TotalRollos + "}";

                            var contentPedido = new StringContent(json, Encoding.UTF8, "application/json");

                            // POST
                            HttpResponseMessage response = await client.PostAsync(url, contentPedido);

                            //Leer respuesta
                            string responseBody = await response.Content.ReadAsStringAsync();

                            log.Info($"[OK] Notificación enviada exitosamente a WEB | OrdenVenta: {OrdenVenta} | StatusCode: {response.StatusCode} | Respuesta: {responseBody}");
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error($"[ERROR] Error al notificar a WEB sobre actualización de rollos | Folio: {RequestData.Folio} | Error: {e.Message}");
                        Console.WriteLine(e.ToString());
                    }

                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Escaneo insertado correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Respuesta generada exitosamente | Status: SI | Folio: {RequestData.Folio}");
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en InsertaLineasVentasHHEMOV, No fue posible insertar lineas en el documento base para la entrega de mercancía | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible insertar lineas en el documento base para la entrega de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosVentasEMOV()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatosVentas.DocumentosVentasEMOV RequestData;

            try
            {
                log.Info("=======================================================================================");
                log.Info("==================ELIMINAR DOCUMENTO ENTREGAS================================");
                log.Info("=======================================================================================");

                // Leer el cuerpo de la solicitud
                string xmlData = string.Empty;

                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                }

                XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.DocumentosVentasEMOV));

                try
                {
                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosVentas.DocumentosVentasEMOV)serializer.Deserialize(stringReader);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[ERROR] Error al deserializar XML | Error: {ex.Message}");
                    throw new Exception($"Error al procesar XML: {ex.Message}");
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                log.Info($"[INFO] El usuario {RequestData.Usuario} intenta elimininar el documento con los siguientes datos | Folio: {RequestData.Folio}");
                log.Info($"[INFO] Preparando parámetros para eliminación | Folio: {RequestData.Folio}");
                parameters.Add("Folio", RequestData.Folio);

                //Generar folio nuevo para OC
                log.Info($"[INFO] Ejecutando procedimiento de eliminación | Procedimiento: {Logic.AD.GCEliminaDocumentosVentasEMOV} | Folio: {RequestData.Folio}");
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosVentasEMOV, parameters);

                if (result.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al eliminar documento | Folio: {RequestData.Folio} | Resultado: {result}");

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "No fue posible elminar el documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Warn($"[WARN] Respuesta de error generada | Folio: {RequestData.Folio}");
                }
                else
                {
                    log.Info($"[OK] Documento eliminado correctamente | Folio: {RequestData.Folio}");

                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    try
                    {
                        List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);
                        log.Info($"[OK] Resultado deserializado correctamente | Registros: {dataList?.Count ?? 0}");

                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "OK",
                            Message = "Documento eliminado correctamente.",
                            Data = dataList
                        };
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error al deserializar resultado JSON | Folio: {RequestData.Folio} | Error: {ex.Message} | Resultado: {result}");
                        throw new Exception($"Error al procesar respuesta: {ex.Message}");
                    }
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                log.Info($"[INFO] Serializando respuesta a XML | Status: {jsonResponse.Status} | Folio: {RequestData.Folio}");
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                log.Info($"[OK] Respuesta XML generada exitosamente | Folio: {RequestData.Folio} | Status: {jsonResponse.Status}");

                return result;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en EliminarDocumentosVentasEMOV | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");

                log.Error($"[ERROR] Detalle completo del error: {Error.ToString()}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible elminar el documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                string errorResponse = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                log.Info($"[INFO] Respuesta de error serializada y retornada");

                return errorResponse;
            }
        }

        [HttpGet] //Listado
        public string GetOrdenesVenta()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("OV", Request.Headers["OV"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetOrdenesVentaHH, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información relacionada con los filtros especificados.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "Error, No fue posible obtener el listado de ordenes de venta: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Ordenes de compra obtenidas correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "Error, No fue posible obtener el listado de ordenes de venta: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado
        public string GetRollosHHEMOV()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("Folio", Request.Headers["Folio"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetRollosHHEMOV, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información relacionada al folio especificado.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "Error, No fue posible obtener el numero de rollos: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Número de rollos obtenido correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "Error, No fue posible obtener el numero de rollos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetArticulosPorOVHH()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            string OV = string.Empty;
            try
            {
                //Parametros de fecha
                OV = Request.Headers["OV"];
                RequestParameters.Add("OV", Request.Headers["OV"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetArticulosOVHH, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información relacionada con los filtros especificados.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    string CustomMessage = (OV != null ? "No fue posible obtener los datos maestros del artículo: " : "No fue posible obtener el listado de artículos por orden de venta");
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = CustomMessage + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Listado de arículos de venta obtenidos correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                string CustomMessage = (OV != null ? "No fue posible obtener los datos maestros del artículo: " : "No fue posible obtener el listado de artículos por orden de venta");

                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = CustomMessage + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosVentasHHEMOV()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetDocumentosVentasHHEMOV, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontraron nuevos documentos.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener el listado de documentos para entrega: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Documentos de entrega obtenidos correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener el listado de documentos para entrega: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetLinesDocumentosVentasHHEMOV()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);
                parameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosVentasHHEMOV, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información asociada.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "OK",
                        Message = "Lista de escaneos obtenidos correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener el detalle del documento: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public async Task<string> EliminaEscaneosVentasHHEMOV()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatosVentas.EscaneosVentasHHEMOV RequestData;

            try
            {
                log.Info("=======================================================================================");
                log.Info("==================ELIMINAR ESCANEOS ENTREGAS================================");
                log.Info("=======================================================================================");

                string xmlData = string.Empty;

                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    xmlData = reader.ReadToEnd(); // Obtener el XML enviado

                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosVentas.EscaneosVentasHHEMOV));

                    try
                    {
                        using (StringReader stringReader = new StringReader(xmlData))
                        {
                            //Deserializar los datos enviados en el modelo
                            RequestData = (AccesoDatosVentas.EscaneosVentasHHEMOV)serializer.Deserialize(stringReader);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error al deserializar XML | Error: {ex.Message}");
                        throw new Exception($"Error al procesar XML: {ex.Message}");
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                log.Info($"[INFO] El usuario {RequestData.Usuario} intenta elimininar el escaneo | ID: {RequestData.ID} para el Documento | Folio: {RequestData.Folio}");
                log.Info($"[INFO] Preparando parámetros para eliminación | ID: {RequestData.ID} | Folio: {RequestData.Folio}");
                parameters.Add("ID", RequestData.ID);
                parameters.Add("Folio", RequestData.Folio);

                //Generar folio nuevo para OC
                log.Info($"[INFO] Ejecutando procedimiento de eliminación | Procedimiento: {Logic.AD.GCEliminaEscaneosVentasHHEMOV} | ID: {RequestData.ID} | Folio: {RequestData.Folio}");
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaEscaneosVentasHHEMOV, parameters);

                if (result.Contains("Error"))
                {
                    log.Error($"[ERROR] Error al eliminar escaneo | ID: {RequestData.ID} | Folio: {RequestData.Folio} | Resultado: {result}");

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "No fue posible eliminar el escaneo del documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Warn($"[WARN] Respuesta de error generada | ID: {RequestData.ID} | Folio: {RequestData.Folio}");
                }
                else
                {
                    log.Info($"[OK] Escaneo eliminado correctamente | ID: {RequestData.ID} | Folio: {RequestData.Folio}");

                    //Notificar a WEB sobre la cantidad de rollos cargados
                    try
                    {
                        log.Info($"[INFO] Iniciando notificación a WEB sobre cantidad de rollos | Folio: {RequestData.Folio}");

                        //Generar folio nuevo para OC
                        parameters.Clear();
                        parameters.Add("Folio", RequestData.Folio);

                        log.Info($"[INFO] Obteniendo cantidad de rollos | Procedimiento: {Logic.AD.GCGetRollosHHEMOV} | Folio: {RequestData.Folio}");
                        string Rollos = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetRollosHHEMOV, parameters);

                        JArray NumRollos = JArray.Parse(Rollos);
                        string TotalRollos = string.Empty;
                        string OrdenVenta = string.Empty;

                        if (NumRollos.Count > 0)
                        {
                            TotalRollos = (string)NumRollos[0]["NumeroRollos"];
                            OrdenVenta = (string)NumRollos[0]["OrdenVenta"];
                            log.Info($"[OK] Cantidad de rollos obtenida | OrdenVenta: {OrdenVenta} | TotalRollos: {TotalRollos} | Folio: {RequestData.Folio}");
                        }
                        else
                        {
                            JArray OV = JArray.Parse(result);
                            TotalRollos = "0";
                            OrdenVenta = (string)OV[0]["Estatus"];
                            log.Warn($"[WARN] No se encontraron rollos, usando valores por defecto | OrdenVenta: {OrdenVenta} | TotalRollos: {TotalRollos} | Folio: {RequestData.Folio}");
                        }

                        using (var client = new HttpClient())
                        {
                            client.Timeout = new TimeSpan(0, 0, 4);
                            //URL de la API
                            string url = ConfigurationManager.AppSettings["EndPointActualizacionRollos"];

                            log.Info($"[INFO] Notificando a WEB | URL: {url} | OrdenVenta: {OrdenVenta} | Rollos: {TotalRollos}");

                            //Datos a enviar
                            var json = "{ \"pedido\": \"" + OrdenVenta + "\", \"rollos\":" + TotalRollos + "}";

                            var contentPedido = new StringContent(json, Encoding.UTF8, "application/json");

                            // POST
                            HttpResponseMessage response = await client.PostAsync(url, contentPedido);

                            //Leer respuesta
                            string responseBody = await response.Content.ReadAsStringAsync();

                            log.Info($"[OK] Notificación a WEB exitosa | StatusCode: {response.StatusCode} | OrdenVenta: {OrdenVenta} | Respuesta: {responseBody}");
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error($"[ERROR] Error al notificar a WEB | Folio: {RequestData.Folio} | Error: {e.Message} | StackTrace: {e.StackTrace}");
                    }

                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    try
                    {
                        List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);
                        log.Info($"[OK] Resultado deserializado correctamente | Registros: {dataList?.Count ?? 0} | Folio: {RequestData.Folio}");

                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "OK",
                            Message = "Escaneo eliminado correctamente.",
                            Data = dataList
                        };
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[ERROR] Error al deserializar resultado JSON | Folio: {RequestData.Folio} | Error: {ex.Message} | Resultado: {result}");
                        throw new Exception($"Error al procesar respuesta: {ex.Message}");
                    }
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                log.Info($"[INFO] Serializando respuesta a XML | Status: {jsonResponse.Status} | Folio: {RequestData.Folio}");
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                log.Info($"[OK] Respuesta XML generada exitosamente | Folio: {RequestData.Folio} | Status: {jsonResponse.Status}");

                return result;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en EliminaEscaneosVentasHHEMOV | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");

                log.Error($"[ERROR] Detalle completo del error: {Error.ToString()}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible elminar el escaneo del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                string errorResponse = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                log.Info($"[INFO] Respuesta de error serializada y retornada");

                return errorResponse;
            }
        }

        [HttpGet]
        public string ValidarLoteVentasHHEMOV()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                RequestParameters.Add("ItemCode", Request.Headers["ItemCode"]);
                RequestParameters.Add("Lote", Request.Headers["Lote"]);
                RequestParameters.Add("WhsOV", Request.Headers["WhsOV"]);
                RequestParameters.Add("UMS", Request.Headers["UMS"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteVentasHHEMOV, RequestParameters);
                result = result.Replace("?", "●");

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información relacionada al lote especificado.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener información relacionada al lote especificado: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Validaciones completadas.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener información relacionada al lote especificado: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string ValidarOrdenVentaHHEMOV()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {

                RequestParameters.Add("DocEntry", Request.Headers["DocEntry"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarOrdenVentaHHEMOV, RequestParameters);
                result = result.Replace("?", "●");
                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información relacionada al documento especificado.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible realizar validaciones de OV: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "OK",
                        Message = "Validaciones completadas.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener información relacionada al documento especificado: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

    }
}