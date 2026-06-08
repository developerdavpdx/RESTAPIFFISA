using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Xml.Serialization;

namespace RESTAPIFFISA.Controllers
{
    public class InventariosMovilController : Controller
    {
        readonly LogicaInventariosMovil Logic = new LogicaInventariosMovil();
        private static readonly ILog log = LogManager.GetLogger("InventariosMovil");

        [HttpPost]
        public async Task<string> GenerarEntradaMercancia()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EntradasMercanciaHHEMD RequestData = new AccesoDatosInventarios.EntradasMercanciaHHEMD();
            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EntradasMercanciaHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.EntradasMercanciaHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                string result = string.Empty;
                // Asegurarse de que se haya iniciado sesión
                GlobalCommands.SapResponse oLoggedIn = null;
                GlobalCommands.SapResponse oEntradaMaercancia = null;
                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = $"No fue posible generar la entrada de mercancía: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {

                    //Metodo de entrada de mercancia aqui
                    oEntradaMaercancia = await Logic.CreateGoodsReceiptAsync(RequestData);

                    //Cerrar sesion
                    await Logic.LoginService.LogoutAsyncHttpClient();

                    if (oEntradaMaercancia.IsError)
                    {
                        parameters.Clear();

                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "NO",
                            Message = $"No fue posible generar la entrada de mercancía: {oEntradaMaercancia.Message}",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oEntradaMaercancia.Message,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString(); ;
                string msg = "No es posible generar la entrada de mercancía: " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
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
        public async Task<string> GenerarSalidaMercancia()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.SalidasMercanciaHHEMD RequestData = new AccesoDatosInventarios.SalidasMercanciaHHEMD();
            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.SalidasMercanciaHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.SalidasMercanciaHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                string result = string.Empty;
                // Asegurarse de que se haya iniciado sesión
                GlobalCommands.SapResponse oLoggedIn = null;
                GlobalCommands.SapResponse oEntradaMaercancia = null;
                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = $"No fue posible generar la salida de mercancía: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {

                    //Metodo de entrada de mercancia aqui
                    oEntradaMaercancia = await Logic.CreateGoodsIssueAsync(RequestData);

                    //Cerrar sesion
                    await Logic.LoginService.LogoutAsyncHttpClient();

                    if (oEntradaMaercancia.IsError)
                    {
                        parameters.Clear();

                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "NO",
                            Message = $"No fue posible generar la salida de mercancía: {oEntradaMaercancia.Message}",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oEntradaMaercancia.Message,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString(); ;
                string msg = "No es posible generar la salida de mercancía: " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
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
        public async Task<string> GenerarTransferenciaStock()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.TraspasosMercanciaHHEMD RequestData = new AccesoDatosInventarios.TraspasosMercanciaHHEMD();
            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.TraspasosMercanciaHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.TraspasosMercanciaHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                string result = string.Empty;
                // Asegurarse de que se haya iniciado sesión
                GlobalCommands.SapResponse oLoggedIn = null;
                GlobalCommands.SapResponse oEntradaMaercancia = null;
                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = $"No fue posible generar la transferencia de stock: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {

                    //Metodo de entrada de mercancia aqui
                    oEntradaMaercancia = await Logic.CreateTransferAsync(RequestData);

                    //Cerrar sesion
                    await Logic.LoginService.LogoutAsyncHttpClient();

                    if (oEntradaMaercancia.IsError)
                    {
                        parameters.Clear();

                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "NO",
                            Message = $"No fue posible generar la transferencia de stock: {oEntradaMaercancia.Message}",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oEntradaMaercancia.Message,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString(); ;
                string msg = "No es posible generar la transferencia de stock: " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
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
        public async Task<string> GenerarRecuentoInventarios()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.RecuentoInventarioHHRI RequestData = new AccesoDatosInventarios.RecuentoInventarioHHRI();

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.RecuentoInventarioHHRI));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        RequestData = (AccesoDatosInventarios.RecuentoInventarioHHRI)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("======================GENERAR RECUENTO INVENTARIOS====================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta generar recuento de inventario | DocEntry: {RequestData.DocEntry} | FolioRI: {RequestData.FolioRI}");

                string result = string.Empty;

                // Iniciar sesión en SAP Service Layer
                GlobalCommands.SapResponse oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();

                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = $"No fue posible generar el recuento de inventario: {oLoggedIn.Message}, No se pudo autenticar en SAP Service Layer.",
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] No fue posible autenticar en SAP Service Layer | Usuario: {RequestData.Usuario} | Error: {oLoggedIn.Message}");
                }
                else
                {
                    log.Info($"[OK] Autenticación exitosa en SAP Service Layer | Usuario: {RequestData.Usuario}");

                    // Ejecutar generación de recuento en SAP
                    GlobalCommands.SapResponse oEntradaMaercancia =
                        await Logic.UpdateInventoryCountingAsync(RequestData);

                    // Cerrar sesión
                    await Logic.LoginService.LogoutAsyncHttpClient();
                    log.Info($"[OK] Sesión cerrada en SAP Service Layer | Usuario: {RequestData.Usuario}");

                    if (oEntradaMaercancia.IsError)
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "NO",
                            Message = (oEntradaMaercancia.Message.Contains("no permite enviar nuevos lotes")
                                        ? oEntradaMaercancia.Message
                                        : "No fue posible generar el recuento de inventario: " + oEntradaMaercancia.Message),
                            Data = new List<Dictionary<string, object>>()
                        };

                        log.Error($"[ERROR] Error al generar recuento de inventario en SAP | Usuario: {RequestData.Usuario} | DocEntry: {RequestData.DocEntry} | Error: {oEntradaMaercancia.Message}");
                    }
                    else
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "SI",
                            Message = oEntradaMaercancia.Message,
                            Data = new List<Dictionary<string, object>>()
                        };

                        log.Info($"[OK] Recuento de inventario generado exitosamente en SAP | Usuario: {RequestData.Usuario} | DocEntry: {RequestData.DocEntry}");
                    }
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = ControllerContext.RouteData.Values["controller"].ToString();
                string msg = "No es posible generar el recuento de inventario: " + MethodName +
                             " en: " + ControllerName +
                             ", por favor contacte al administrador del sistema con el siguiente código de error: ";

                string finalmessage = Logic.GlobalCommands.Excepcion(ex, msg).ToString();

                log.Error($"[ERROR] Excepción crítica en GenerarRecuentoInventarios | Usuario: {RequestData?.Usuario} | DocEntry: {RequestData?.DocEntry} | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

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


        [HttpGet]
        public string GetPlantillas()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                parameters.Add("Usuario", Request.Headers["Usuario"]);
                string PlantillaEMD = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetPlantillaEtiquetaHHEMD, parameters); //Obtener la plantilla seleccionada desde DB
                JArray PlantillasData = JArray.Parse(PlantillaEMD);

                // Ruta donde están los archivos de plantillas
                string rutaPlantillas = ConfigurationManager.AppSettings["PlantillasRuta"];

                // Verificar si la carpeta existe
                if (!Directory.Exists(rutaPlantillas))
                {
                    return JsonConvert.SerializeObject(new { error = "La ruta de plantillas no existe" });
                }

                // Obtener todos los archivos .txt de la carpeta
                string[] archivos = Directory.GetFiles(rutaPlantillas, "*.txt");

                // Extraer solo los nombres de archivo
                List<Dictionary<string, object>> nombresArchivos = new List<Dictionary<string, object>>();

                foreach (string archivo in archivos)
                {
                    string nombreArchivo = Path.GetFileNameWithoutExtension(archivo);
                    Dictionary<string, object> dict = new Dictionary<string, object>
                    {
                        { "plantilla", nombreArchivo },
                        { "PlantillaSeleccionada", (PlantillasData.Count > 0 ? PlantillasData[0]["Plantilla"] : string.Empty) },
                        { "Usuario", (PlantillasData.Count > 0 ? PlantillasData[0]["Usuario"] : string.Empty) },
                        { "Selected", (nombreArchivo == (string)(PlantillasData.Count > 0 ? PlantillasData[0]["Plantilla"] : string.Empty) ? "SI" : "NO") }
                    };
                    nombresArchivos.Add(dict);
                }

                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "OK",
                    Message = "Lista de plantillas obtenidas correctamente",
                    Data = nombresArchivos
                };

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string result = Logic.GlobalCommands.SerializeToXml(jsonResponse);

                return result;
            }
            catch (Exception ex)
            {

                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener la lista de plantillas: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado
        public string ValidaAlmacenHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("Almacen", Request.Headers["Almacen"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidaAlmacenHHEMD, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "El almacén especificado no existe, para buscar coloque el código de almacén y presione el botón con el icono verde.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "Error, No fue posible validar el almacén: " + result,
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
                        Message = "Almacén validado correctamente.",
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
                    Message = "Error, No fue posible validar el almacén: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado
        public string RequiereMetros()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("ItemCode", Request.Headers["ItemCode"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCRequiereCantidadMetrosHHEMD, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "El artículo especificado no existe, no es posible validar si el artículo requiere cantidad en metros, intente de nuevo.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "Error, no fue posible validar si el artículo requiere cantidad en metros, intente de nuevo: " + result,
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
                        Message = "Artículo validado correctamente.",
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
                    Message = "Error, no fue posible validar si el artículo requiere cantidad en metros, intente de nuevo.: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado con paginación
        public string GetArticulosHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse2 jsonResponse;

            try
            {
                // Parámetros de búsqueda
                string busqueda = Request.Headers["Busqueda"];

                // Parámetros de paginación (opcionales)
                int pageNumber = 1;
                int pageSize = 50; // valor por defecto

                if (Request.Headers["PaginaActual"] != null &&
                    int.TryParse(Request.Headers["PaginaActual"], out int pn))
                {
                    pageNumber = pn > 0 ? pn : 1;
                }
                if (Request.Headers["ArticulosPorPagina"] != null &&
                    int.TryParse(Request.Headers["ArticulosPorPagina"], out int ps))
                {
                    pageSize = ps > 0 ? ps : 50;
                }

                RequestParameters.Add("Busqueda", busqueda);

                // Obtener todos los datos (idealmente aquí se debe filtrar / paginar en BD)
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetArticulosHHEMD, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "NO",
                        Message = "No se encontró información relacionada a los criterios de búsqueda.",
                        TotalRegistros = string.Empty,
                        TotalPaginas = string.Empty,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "ERROR",
                        Message = "No fue posible obtener el listado de artículos: " + result,
                        TotalRegistros = string.Empty,
                        TotalPaginas = string.Empty,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    // Aplicar paginación local
                    int totalItems = dataList.Count;
                    int skip = (pageNumber - 1) * pageSize;

                    List<Dictionary<string, object>> pageData = new List<Dictionary<string, object>>();
                    if (skip < totalItems)
                    {
                        pageData = dataList.Skip(skip).Take(pageSize).ToList();
                    }

                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "OK",
                        Message = $"Listado de artículos obtenido correctamente.",
                        TotalRegistros = totalItems.ToString(),
                        TotalPaginas = Math.Ceiling((double)totalItems / pageSize).ToString(),
                        Data = pageData
                    };
                }

                // Serializar y retornar XML
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse2()
                {
                    Status = "ERROR",
                    Message = "No fue posible obtener el listado de artículos: " + ex.ToString(),
                    TotalRegistros = string.Empty,
                    TotalPaginas = string.Empty,
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado con paginación
        public string GetCuentasContablesHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse2 jsonResponse;

            try
            {
                // Parámetros de paginación (opcionales)
                int pageNumber = 1;
                int pageSize = 50; // valor por defecto

                // Parámetros de búsqueda
                string busqueda = Request.Headers["Busqueda"];

                if (Request.Headers["PaginaActual"] != null &&
                    int.TryParse(Request.Headers["PaginaActual"], out int pn))
                {
                    pageNumber = pn > 0 ? pn : 1;
                }
                if (Request.Headers["ArticulosPorPagina"] != null &&
                    int.TryParse(Request.Headers["ArticulosPorPagina"], out int ps))
                {
                    pageSize = ps > 0 ? ps : 50;
                }

                RequestParameters.Add("Busqueda", busqueda);

                // Obtener todos los datos (idealmente aquí se debe filtrar / paginar en BD)
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetCuentasContablesHHEMD, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "NO",
                        Message = "No se encontró información relacionada a los criterios de búsqueda.",
                        TotalRegistros = string.Empty,
                        TotalPaginas = string.Empty,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "ERROR",
                        Message = "No fue posible obtener el listado de cuentas contables: " + result,
                        TotalRegistros = string.Empty,
                        TotalPaginas = string.Empty,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    // Aplicar paginación local
                    int totalItems = dataList.Count;
                    int skip = (pageNumber - 1) * pageSize;

                    List<Dictionary<string, object>> pageData = new List<Dictionary<string, object>>();
                    if (skip < totalItems)
                    {
                        pageData = dataList.Skip(skip).Take(pageSize).ToList();
                    }

                    jsonResponse = new AccesoDatos.JsonResponse2()
                    {
                        Status = "OK",
                        Message = $"Listado de cuentas contables obtenidas correctamente.",
                        TotalRegistros = totalItems.ToString(),
                        TotalPaginas = Math.Ceiling((double)totalItems / pageSize).ToString(),
                        Data = pageData
                    };
                }

                // Serializar y retornar XML
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse2()
                {
                    Status = "ERROR",
                    Message = "No fue posible obtener el listado de cuentas contables: " + ex.ToString(),
                    TotalRegistros = string.Empty,
                    TotalPaginas = string.Empty,
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet] //Listado
        public string GetRecuentosInventarioHHRI()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("Recuento", Request.Headers["Recuento"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetRecuentosInventarioHHRI, RequestParameters);

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
                        Message = "Error, No fue posible obtener el listado de recuentos de inventario: " + result,
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
                    Message = "Error, No fue posible obtener el listado de recuentos de inventario: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosEntradasHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetDocumentosEntradasHHEMD, RequestParameters);

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
                        Message = "No fue posible obtener el listado de documentos para entradas: " + result,
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
                        Message = "Documentos de entradas obtenidos correctamente.",
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
                    Message = "No fue posible obtener el listado de documentos para entradas: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosSalidasHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetDocumentosSalidasHHEMD, RequestParameters);

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
                        Message = "No fue posible obtener el listado de documentos para salidas: " + result,
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
                        Message = "Documentos de entradas obtenidos correctamente.",
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
                    Message = "No fue posible obtener el listado de documentos para salidas: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosTransferenciasHHTS()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGeDocumentosTransferenciasHHTS, RequestParameters);

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
                        Message = "No fue posible obtener el listado de documentos para transferencias: " + result,
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
                        Message = "Documentos de entradas obtenidos correctamente.",
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
                    Message = "No fue posible obtener el listado de documentos para transferencias: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetTotalTransferenciasHHRI()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FolioTS", Request.Headers["FolioTS"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetTotalTraspasosHHRI, RequestParameters);

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
                        Message = "Error, No fue posible obtener el total del traspasos: " + result,
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
                        Message = "Total de traspasos obtenido correctamente.",
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
                    Message = "Error, No fue posible obtener el total del traspasos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosRecuentosHHRI()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGeDocumentosRecuentosHHRI, RequestParameters);

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
                        Message = "No fue posible obtener el listado de documentos para recuentos: " + result,
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
                        Message = "Documentos de entradas obtenidos correctamente.",
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
                    Message = "No fue posible obtener el listado de documentos para recuentos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetLinesDocumentosEntradasHHEMD()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);
                parameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosEntradasHHEMD, parameters);

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

        [HttpGet]
        public string GetLinesDocumentosSalidasHHEMD()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);
                parameters.Add("Usuario", Request.Headers["Usuario"]);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosSalidasHHEMD, parameters);

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

        [HttpGet]
        public string GetLinesDocumentosTransferenciasHHTS()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);
                parameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosTransferenciasHHTS, parameters);

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
        [HttpGet]
        public string GetLinesDocumentosRecuentosHHRI()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);
                parameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosRecuentosHHRI, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontró información asociada, no se realizó ningún escaneo para el documento.",
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

        [HttpGet]
        public string GetTotalRecuentoHHRI()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("FolioRI", Request.Headers["FolioRI"]);
                RequestParameters.Add("Usuario", Request.Headers["Usuario"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetTotalRecuentoHHRI, RequestParameters);

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
                        Message = "Error, No fue posible obtener el total del recuento: " + result,
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
                        Message = "Total de recuento obtenido correctamente.",
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
                    Message = "Error, No fue posible obtener el total del recuento: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string ValidarLoteInventariosHH()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {

                RequestParameters.Add("ItemCode", Request.Headers["ItemCode"]);
                RequestParameters.Add("Lote", Request.Headers["Lote"]);
                RequestParameters.Add("Folio", Request.Headers["Folio"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteInventariosHH, RequestParameters);
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
        public string ValidarLoteInventariosHHEMD()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {

                RequestParameters.Add("ItemCode", Request.Headers["ItemCode"]);
                RequestParameters.Add("Lote", Request.Headers["Lote"]);
                RequestParameters.Add("Folio", Request.Headers["Folio"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteInventariosHHEMD, RequestParameters);
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
        public async Task<string> ValidarLoteRecuentoHHRI()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            GlobalCommands.SapResponse oLoggedIn = null;

            try
            {
                // 🔹 1. Obtener parámetros desde los encabezados
                string lote = Request.Headers["Lote"];
                string docEntry = Request.Headers["DocEntry"]; // ⚠️ Enviar este encabezado desde el móvil o UI
                string docNum = Request.Headers["DocNum"];

                if (string.IsNullOrWhiteSpace(lote) || string.IsNullOrWhiteSpace(docEntry))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "Los parámetros 'Lote' y 'DocEntry' son obligatorios.",
                        Data = new List<Dictionary<string, object>>()
                    };
                    return Logic.GlobalCommands.SerializeToXml(jsonResponse);
                }

                // 🔹 2. Iniciar sesión en SAP
                oLoggedIn = await Logic.LoginService.LoginAsyncHttpClient();
                if (oLoggedIn.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = $"No fue posible autenticar en SAP Service Layer: {oLoggedIn.Message}",
                        Data = new List<Dictionary<string, object>>()
                    };
                    return Logic.GlobalCommands.SerializeToXml(jsonResponse);
                }

                // 🔹 3. GET del recuento en SAP
                var getUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/InventoryCountings({docEntry})";
                var getResponse = await Logic.LoginService._httpClient.GetAsync(getUrl);

                if (!getResponse.IsSuccessStatusCode)
                {
                    await Logic.LoginService.LogoutAsyncHttpClient();

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = $"No se pudo obtener el recuento {docEntry} desde SAP.",
                        Data = new List<Dictionary<string, object>>()
                    };
                    return Logic.GlobalCommands.SerializeToXml(jsonResponse);
                }

                string jsonResult = await getResponse.Content.ReadAsStringAsync();
                dynamic recuentoExistente = JsonConvert.DeserializeObject(jsonResult);

                // 🔹 4. Buscar si el lote ya fue contado en alguna línea del recuento
                bool loteEncontrado = false;
                string itemEncontrado = string.Empty;
                string almacenEncontrado = string.Empty;

                foreach (var linea in recuentoExistente.InventoryCountingLines)
                {
                    if (linea.InventoryCountingBatchNumbers != null)
                    {
                        foreach (var batch in linea.InventoryCountingBatchNumbers)
                        {
                            string batchNum = ((string)batch.BatchNumber).Trim();
                            if (batchNum.Equals(lote.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                loteEncontrado = true;
                                itemEncontrado = linea.ItemCode;
                                almacenEncontrado = linea.WarehouseCode;
                                break;
                            }
                        }
                    }

                    if (loteEncontrado)
                        break;
                }

                // 🔹 5. Cerrar sesión en SAP
                await Logic.LoginService.LogoutAsyncHttpClient();

                // 🔹 6. Armar respuesta
                if (loteEncontrado)
                {
                    string CustomMessage = $"El lote {lote} ya fue contado en el recuento {docNum} (Artículo: {itemEncontrado}, Almacén: {almacenEncontrado}).";

                    string result = $@"[{{""Codigo"":""LOTE_CONTADO"",""Mensaje"":""{CustomMessage}"",""CodigoAlmacen"":"""",""NombreAlmacen"":"""",""Articulo"":"""",""Lote"":"""",""Cantidad"":0.00,""Cantidadenmetros"":0.000000}}]";
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "OK",
                        Message = $"Validaciones completadas.",
                        Data = dataList
                    };


                }
                else
                {
                    RequestParameters.Add("Lote", Request.Headers["Lote"]);
                    RequestParameters.Add("DocNum", Request.Headers["DocNum"]);

                    string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteRecuentoHHRI, RequestParameters);
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
                }

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
            catch (Exception ex)
            {
                // 🔹 7. Logout seguro si algo falla
                if (oLoggedIn != null && !oLoggedIn.IsError)
                    await Logic.LoginService.LogoutAsyncHttpClient();

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = $"Error al validar el lote: {ex.Message}",
                    Data = new List<Dictionary<string, object>>()
                };
                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }


        //[HttpGet]
        //public string ValidarLoteRecuentoHHRI()
        //{
        //    Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
        //    AccesoDatos.JsonResponse jsonResponse;

        //    try
        //    {

        //        RequestParameters.Add("Lote", Request.Headers["Lote"]);
        //        RequestParameters.Add("DocNum", Request.Headers["DocNum"]);

        //        string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteRecuentoHHRI, RequestParameters);
        //        result = result.Replace("?", "●");

        //        if (result == "[]")
        //        {
        //            jsonResponse = new AccesoDatos.JsonResponse()
        //            {

        //                Status = "NO",
        //                Message = "No se encontró información relacionada al lote especificado.",
        //                Data = new List<Dictionary<string, object>>()
        //            };
        //        }
        //        else if (result.Contains("Error"))
        //        {
        //            jsonResponse = new AccesoDatos.JsonResponse()
        //            {

        //                Status = "ERROR",
        //                Message = "No fue posible obtener información relacionada al lote especificado: " + result,
        //                Data = new List<Dictionary<string, object>>()
        //            };
        //        }
        //        else
        //        {
        //            // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
        //            List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

        //            jsonResponse = new AccesoDatos.JsonResponse()
        //            {

        //                Status = "OK",
        //                Message = "Validaciones completadas.",
        //                Data = dataList
        //            };
        //        }

        //        // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
        //        result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
        //        return result;

        //    }
        //    catch (Exception ex)
        //    {
        //        jsonResponse = new AccesoDatos.JsonResponse()
        //        {

        //            Status = "ERROR",
        //            Message = "No fue posible obtener información relacionada al lote especificado: " + ex.ToString(),
        //            Data = new List<Dictionary<string, object>>()
        //        };

        //        return Logic.GlobalCommands.SerializeToXml(jsonResponse);
        //    }
        //}

        [HttpGet]
        public string ValidarLoteTraspasosHHTM()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {

                RequestParameters.Add("ItemCode", Request.Headers["ItemCode"]);
                RequestParameters.Add("Lote", Request.Headers["Lote"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarLoteTraspasosHHTM, RequestParameters);
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

        //Inserta el encabezado de la orden de venta para generar la Entrega de mercancia
        [HttpPost]
        public string UpdatePlantillaEtiquetaHHEMD()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.PlantillasEtiquetaEMD RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.PlantillasEtiquetaEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.PlantillasEtiquetaEMD)serializer.Deserialize(stringReader);
                    }
                }

                parameters.Add("Plantilla", RequestData.Plantilla);
                parameters.Add("Usuario", RequestData.Usuario);
                string PlantillaEMD = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCUpdatePlantillaEtiquetaHHEMD, parameters);


                if (PlantillaEMD.Contains("Error") || PlantillaEMD.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible guardar la configuración de plantilla de impresión: " + PlantillaEMD,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(PlantillaEMD);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Configuración generada correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible guardar la configuración de plantilla de impresión: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta el encabezado de la orden de venta para generar la entrada de mercancia
        [HttpPost]
        public string InsertaEncabezadoEntradasHHEMD()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EncabezadoEntradasHHEMD RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EncabezadoEntradasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.EncabezadoEntradasHHEMD)serializer.Deserialize(stringReader);
                    }
                }


                parameters.Add("Usuario", RequestData.Usuario);
                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaEncabezadoEntradasHHEMD, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible generar el documento base para la entrada de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);


                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible generar el documento base para la entrada de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }


        //Inserta el encabezado de la orden de venta para generar la entrada de mercancia
        [HttpPost]
        public string InsertaEncabezadoSalidasHHEMD()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EncabezadoSalidasHHEMD RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EncabezadoSalidasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.EncabezadoSalidasHHEMD)serializer.Deserialize(stringReader);
                    }
                }


                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaEncabezadoSalidasHHEMD, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible generar el documento base para la salida de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);


                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible generar el documento base para la salida de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta el encabezado de la orden de venta para generar la transferencia de stock
        [HttpPost]
        public string InsertaEncabezadoTransferenciasHHTS()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EncabezadoTransferenciasHHTS RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EncabezadoTransferenciasHHTS));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.EncabezadoTransferenciasHHTS)serializer.Deserialize(stringReader);
                    }
                }


                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaEncabezadoTransferenciasHHTS, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible generar el documento base para la transferencia de stock, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);


                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible generar el documento base para la transferencia de stock: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string InsertaEncabezadoRecuentosHHRI()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EncabezadoRecuentosHHRI RequestData;

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EncabezadoRecuentosHHRI));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        // Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.EncabezadoRecuentosHHRI)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("=======================ENCABEZADO DOCUMENTO RECUENTOS==================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta generar nuevo documento base para recuento de inventarios.");

                parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                // Generar folio nuevo para Recuento
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(
                    Logic.AD.GCInsertaEncabezadoRecuentosHHRI, parameters);

                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible generar el documento base para el recuento de inventarios, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] Error al generar documento base para recuento de inventarios | Usuario: {RequestData.Usuario} | Error: {folioEM}");
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList =
                        JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "Solicitud procesada correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Documento base de recuento generado exitosamente | Usuario: {RequestData.Usuario} | Folio: {folioEM}");
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en InsertaEncabezadoRecuentosHHRI, No fue posible generar el documento base para el recuento de inventarios | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible generar el documento base para el recuento de inventarios: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }


        //Inserta las lineas para los documentos de entrada de mercancia
        [HttpPost]
        public string InsertaLineasEntradasHHEMD()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.LineasEntradasHHEMD RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.LineasEntradasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.LineasEntradasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaLineasEntradasHHEMD, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible insertar lineas en el documento base para la entrada de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = $"El artículo {RequestData.Articulo} con lote {RequestData.Lote} fue insertado correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible insertar lineas en el documento base para la entrada de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta las lineas para los documentos de salida de mercancia
        [HttpPost]
        public string InsertaLineasSalidasHHEMD()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.LineasSalidasHHEMD RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.LineasSalidasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.LineasSalidasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaLineasSalidasHHEMD, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible insertar lineas en el documento base para la salida de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = $"El artículo {RequestData.Articulo} con lote {RequestData.Lote} fue insertado correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible insertar lineas en el documento base para la salida de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta las lineas para los documentos de salida de mercancia
        [HttpPost]
        public string InsertaLineasTraspasosHHEMD()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.LineasTransferenciasHHTS RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.LineasTransferenciasHHTS));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatosInventarios.LineasTransferenciasHHTS)serializer.Deserialize(stringReader);
                    }
                }

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaLineasTraspasosHHEMD, parameters);


                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible insertar lineas en el documento base para el traspaso de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    parameters.Clear();
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = $"El registro fue insertado correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible insertar lineas en el documento base para el traspaso de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta las lineas para los documentos de salida de mercancia
        [HttpPost]
        public string InsertaLineasRecuentosHHRI()
        {
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.LineasRecuentosHHRI RequestData;

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.LineasRecuentosHHRI));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        // Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.LineasRecuentosHHRI)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("==========================LINEAS DOCUMENTO RECUENTOS==================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario {RequestData.Usuario} intenta insertar líneas para el documento de recuento | Folio: {RequestData.FolioRI}");

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                // Ejecutar SP para insertar líneas
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(
                    Logic.AD.GCInsertaLineasRecuentosHHRI, parameters);

                if (folioEM.Contains("Error") || folioEM.Contains("[]"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible insertar líneas en el documento base para el recuento de mercancía, no fue posible realizar el registro: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] Error al insertar líneas en documento de recuento | Usuario: {RequestData.Usuario} | Folio: {RequestData.FolioRI} | Error: {folioEM}");
                }
                else
                {
                    // 🔹 Convertir JSON a lista de diccionarios
                    List<Dictionary<string, object>> dataList =
                        JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(folioEM);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "SI",
                        Message = "El registro fue insertado correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Líneas insertadas correctamente en documento de recuento | Usuario: {RequestData.Usuario} | Folio: {RequestData.FolioRI}");
                }

                string FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;
            }
            catch (Exception ex)
            {
                log.Error($"[ERROR] Excepción crítica en InsertaLineasRecuentosHHRI, No fue posible insertar líneas en el documento base para el recuento de mercancía | Error: {ex.Message} | StackTrace: {ex.StackTrace}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible insertar líneas en el documento base para el recuento de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }


        [HttpPost]
        public string EliminaEscaneosEntradasHHEMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.EscaneosEntradasHHEMD RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EscaneosEntradasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.EscaneosEntradasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaEscaneosEntradasHHEMD, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible eliminar el registro del documento: " + result,
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
                        Message = "Registro eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el registro del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminaEscaneosSalidasHHEMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.EscaneosSalidasHHEMD RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EscaneosSalidasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.EscaneosSalidasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaEscaneosSalidasHHEMD, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible eliminar el registro del documento: " + result,
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
                        Message = "Registro eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el registro del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminaEscaneosTransferenciasHHTS()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.EscaneosTransferenciasHHTS RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EscaneosTransferenciasHHTS));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.EscaneosTransferenciasHHTS)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaEscaneosTransferenciasHHTS, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible eliminar el registro del documento: " + result,
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
                        Message = "Registro eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el registro del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminaEscaneosRecuentosHHRI()
        {
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.EscaneosRecuentosHHRI RequestData = new AccesoDatosInventarios.EscaneosRecuentosHHRI();

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.EscaneosRecuentosHHRI));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        // Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.EscaneosRecuentosHHRI)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("======================ELIMINAR ESCANEOS RECUENTOS=====================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta eliminar escaneo | Folio: {RequestData.Folio} | ID: {RequestData.ID}");

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                // Ejecutar SP para eliminar escaneo
                string result = Logic.GlobalCommands.ExecuteProcedure(
                    Logic.AD.GCEliminaEscaneosRecuentosHHRI, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "No fue posible eliminar el registro del documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] Error al eliminar escaneo | Usuario: {RequestData.Usuario} | Folio: {RequestData.Folio} | ID: {RequestData.ID} | Error: {result}");
                }
                else
                {
                    List<Dictionary<string, object>> dataList =
                        JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "OK",
                        Message = "Registro eliminado correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Escaneo eliminado correctamente | Usuario: {RequestData.Usuario} | Folio: {RequestData.Folio} | ID: {RequestData.ID}");
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");

                log.Error($"[ERROR] Excepción crítica en EliminaEscaneosRecuentosHHRI, No fue posible eliminar el escaneo | Usuario: {RequestData?.Usuario} | Folio: {RequestData?.Folio} | ID: {RequestData?.ID} | Error: {Error}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible eliminar el registro del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosEntradasHHEMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.DocumentosEntradasHHEMD RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.DocumentosEntradasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.DocumentosEntradasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosEntradasHHEMD, parameters);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar el documento: " + result,
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
                        Message = "Documento eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosVaciosEntradasHHEMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();

            try
            {
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosVaciosEntradasHHEMD, null);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar los documentos temporales: " + result,
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
                        Message = "Documentos eliminados correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar los documentos vacios: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosSalidasHHEMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.DocumentosSalidasHHEMD RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.DocumentosSalidasHHEMD));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.DocumentosSalidasHHEMD)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosSalidasHHEMD, parameters);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar el documento: " + result,
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
                        Message = "Documento eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
        [HttpPost]
        public string EliminarDocumentosVaciosSalidasHHSMD()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();

            try
            {
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosVaciosSalidasHHSMD, null);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar los documentos temporales: " + result,
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
                        Message = "Documentos eliminados correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar los documentos vacios: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosTransferenciasHHTS()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();
            AccesoDatosInventarios.DocumentosTransferenciasHHTS RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.DocumentosTransferenciasHHTS));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.DocumentosTransferenciasHHTS)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosTransferenciasHHTS, parameters);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar el documento: " + result,
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
                        Message = "Documento eliminado correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar el documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
        [HttpPost]
        public string EliminarDocumentosVaciosTransferenciasHHTS()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();

            try
            {
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosVaciosTransferenciasHHTS, null);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar los documentos temporales: " + result,
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
                        Message = "Documentos eliminados correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar los documentos vacios: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosRecuentosHHRI()
        {
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatosInventarios.DocumentosRecuentosHHRI RequestData = new AccesoDatosInventarios.DocumentosRecuentosHHRI();

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatosInventarios.DocumentosRecuentosHHRI));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        // Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatosInventarios.DocumentosRecuentosHHRI)serializer.Deserialize(stringReader);
                    }
                }

                log.Info("=======================================================================================");
                log.Info("=====================ELIMINAR DOCUMENTO RECUENTOS=====================================");
                log.Info("=======================================================================================");
                log.Info($"[OK] El usuario: {RequestData.Usuario} intenta eliminar documento de recuento | Folio: {RequestData.Folio}");

                Dictionary<string, string> parameters = Logic.GlobalCommands.ConvertToParameters(RequestData);

                // Ejecutar SP para eliminar documento
                string result = Logic.GlobalCommands.ExecuteProcedure(
                    Logic.AD.GCEliminaDocumentosRecuentosHHRI, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "No fue posible eliminar el documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };

                    log.Error($"[ERROR] Error al eliminar documento de recuento | Usuario: {RequestData.Usuario} | Folio: {RequestData.Folio} | Error: {result}");
                }
                else
                {
                    List<Dictionary<string, object>> dataList =
                        JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "OK",
                        Message = "Documento eliminado correctamente.",
                        Data = dataList
                    };

                    log.Info($"[OK] Documento de recuento eliminado correctamente | Usuario: {RequestData.Usuario} | Folio: {RequestData.Folio}");
                }

                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");

                log.Error($"[ERROR] Excepción crítica en EliminarDocumentosRecuentosHHRI, No fue posible eliminar el documento | Usuario: {RequestData?.Usuario} | Folio: {RequestData?.Folio} | Error: {Error}");

                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = "No fue posible eliminar el documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosVaciosRecuentosHHRI()
        {
            AccesoDatos.JsonResponse jsonResponse;
            _ = new Dictionary<string, string>();

            try
            {
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosVaciosRecuentosHHRI, null);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar los documentos temporales: " + result,
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
                        Message = "Documentos eliminados correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                Error.Append(ex.Message ?? "");
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : "");
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible elminar los documentos vacios: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
    }
}