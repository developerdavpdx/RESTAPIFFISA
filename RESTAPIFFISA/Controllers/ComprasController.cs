using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Xml.Serialization;

namespace RESTAPIFFISA.Controllers
{
    public class ComprasController : Controller
    {
        readonly Logica Logic = new Logica();

        [HttpGet]
        public string SeriesNumeracion()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                string ObjectCode = Request.Headers["ObjectCode"];


                parameters.Add("ObjectCode", ObjectCode);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetSeriesNumeraciondDocs, parameters);
                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener las series de numeración. no se encontró información asociada al código.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener las series de numeración: " + result,
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
                        Message = "Series de numeración obtenidas correctamente.",
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
                    Message = "No fue posible obtener las series de numeración: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetLinesOrdenesCompra()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                string OrdenCompra = Request.Headers["OrdenCompra"];


                parameters.Add("OrdenCompra", OrdenCompra);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesOrdenesCompra, parameters);
                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener las líneas de la OC. no se encontró información asociada a la orden.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener las líneas de la OC: " + result,
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
                        Message = "Líneas de orden obtenidas correctamente.",
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
                    Message = "No fue posible obtener las líneas de orden: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetOrdenesCompra()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                //Parametros de fecha
                RequestParameters.Add("Busqueda", Request.Headers["Busqueda"]);
                RequestParameters.Add("FI", Request.Headers["FI"]);
                RequestParameters.Add("FF", Request.Headers["FF"]);
                RequestParameters.Add("Series", Request.Headers["Series"]);
                RequestParameters.Add("OC", Request.Headers["OC"]);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCOrdenesCompra, RequestParameters);

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
                        Message = "No fue posible obtener el listado de ordenes de compra: " + result,
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
                    Message = "No fue posible obtener el listado de ordenes de compra: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDetailsOrdenesCompra()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                parameters.Add("DocEntry", Request.Headers["DocEntry"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetDetailsOrdenesCompra, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener el detalle de la orden de compra, no se encontró información relacionada.",
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
                        Message = "Detalle de orden de compra obtenida correctamente.",
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
                    Message = "No fue posible obtener el detalle de la orden de compra: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string UpdateDetailsOrdenesCompra()
        {
            Dictionary<string, string> RequestParameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatos.OcCheckList RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.OcCheckList));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.OcCheckList)serializer.Deserialize(stringReader);

                    }
                }

                RequestParameters.Add("DocEntry", RequestData.DocEntry);
                RequestParameters.Add("CertificadoCalidad", RequestData.CertificadoCalidad);
                RequestParameters.Add("OrdenFisica", RequestData.OrdenFisica);
                RequestParameters.Add("PackingList", RequestData.PackingList);
                RequestParameters.Add("Pedimento", RequestData.Pedimento);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCUpdateDetailsOrdenesCompra, RequestParameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible actualizar el detalle de la orden de compra, no se encontró información relacionada.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible actualizar el detalle de la orden de compra: " + result,
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
                        Message = "Detalle de orden de compra actualizada correctamente.",
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
                    Message = "No fue posible actualizar el detalle de la orden de compra: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public async Task<string> CreateEMByOC()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatos.MultipleEntradaMercanciaByOC RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.MultipleEntradaMercanciaByOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.MultipleEntradaMercanciaByOC)serializer.Deserialize(stringReader);
                    }
                }
                // 🔐 Intenta iniciar sesión con Service Layer
                var login = await Logic.LoginService.LoginAsyncHttpClient();

                string result;
                if (login.IsError)
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = $"No fue posible generar la entrada de mercancía: Login SAP fallido: {login.Message}",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    result = await Logic.CrearEntradaMercanciaDesdeOC(RequestData.Folio, RequestData.OrdenCompra, RequestData.Fecha);

                    await Logic.LoginService.LogoutAsyncHttpClient();

                    if (result == "[]")
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {

                            Status = "NO",
                            Message = "No fue posible generar la entrada de mercancía, no se encontró información relacionada.",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else if (result.Contains("Error"))
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {

                            Status = "ERROR",
                            Message = "No fue posible generar la entrada de mercancía: " + result,
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
                            Message = "Solicitud procesada correctamente.",
                            Data = dataList
                        };
                    }
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
                    Message = "No fue posible generar la entrada de mercancia: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string CreateEMD()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatos.MultipleEntradaMercanciaByOC RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.MultipleEntradaMercanciaByOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.MultipleEntradaMercanciaByOC)serializer.Deserialize(stringReader);
                    }
                }

                string result = Logic.CreateEMDirecta(RequestData.Folio);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible generar la entrada de mercancía directa, no se encontró información relacionada.",
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
                        Message = "Solicitud procesada correctamente.",
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
                    Message = "No fue posible generar la entrada de mercancia directa: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta el encabezado y lineas de la orden de compra para generar la EM
        [HttpPost]
        public string InsertaDocumentosHHOC()
        {
            string FinalResult = string.Empty;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatos.EntradaMercanciaByOC RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.EntradaMercanciaByOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.EntradaMercanciaByOC)serializer.Deserialize(stringReader);
                    }
                }
                string folioEM;
                //Si es la primera vez que se crea el plan de entradas
                if (RequestData.Folio == string.Empty)
                {
                    parameters.Add("OrdenCompra", RequestData.OrdenCompra);
                    parameters.Add("SapDocument", RequestData.SapDocument);
                    parameters.Add("Estatus", "Pendiente");
                    parameters.Add("Autorizacion", "SI");
                    //Generar folio nuevo para OC
                    folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaDocumentosHHOC, parameters);

                    JArray Folio = JArray.Parse(folioEM);

                    folioEM = Folio[0]["Folio"].ToString();
                }
                else
                {
                    //Usar folio anterior de OC
                    folioEM = RequestData.Folio;
                }

                parameters.Clear();
                parameters.Add("Folio", folioEM);
                parameters.Add("Articulo", RequestData.Articulo);
                parameters.Add("Linea", RequestData.Linea);
                parameters.Add("Lote", RequestData.Lote);
                parameters.Add("Cantidad", RequestData.Cantidad);
                string DetailResult = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaDocumentosHHOCDetails, parameters);
                //Generar registro inicial de lineas
                // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(DetailResult);


                if (FinalResult.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible generar el documento para la entrada de mercancía, no fue posible realizar el registro, intentar de nuevo más tarde.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = (DetailResult.Contains("Duplicado") ? "NO" : "SI"),
                        Message = (DetailResult.Contains("Duplicado") ? "Ya has registrado este artículo para la entrada en el folio: " + folioEM : "Solicitud procesada correctamente."),
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                FinalResult = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return FinalResult;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible generar el documento para la entrada de mercancía: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetDocumentosHHOC()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                parameters.Add("FI", Request.Headers["FI"]);
                parameters.Add("FF", Request.Headers["FF"]);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetDocumentosHHOC, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No se encontraron documentos nuevos.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener la lista de documentos: " + result,
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
                        Message = "Lista de documentos obtenidos correctamente.",
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
                    Message = "No fue posible obtener la lista de documentos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarEscaneosHHOC()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.EscaneosHHOC RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.EscaneosHHOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatos.EscaneosHHOC)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                parameters.Add("ID", RequestData.ID);
                parameters.Add("Folio", RequestData.Folio);
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaEscaneosHHOC, parameters);

                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible eliminar el escaneo del documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible eliminar el escaneo del documento: " + result,
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
                        Message = "Escaneo eliminado correctamente.",
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
                    Message = "No fue posible elminar el escaneo del documento: " + Error.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string EliminarDocumentosHHOC()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.DocumentosHHOC RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.DocumentosHHOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo
                        RequestData = (AccesoDatos.DocumentosHHOC)serializer.Deserialize(stringReader);
                    }
                }

                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                parameters.Add("Folio", RequestData.Folio);
                //Generar folio nuevo para OC
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCEliminaDocumentosHHOC, parameters);
                if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible elminar el documento: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }

                else if (result.Contains("Error"))
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

        [HttpGet]
        public string GetLinesDocumentosHHOC()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            try
            {

                parameters.Add("Folio", Request.Headers["Folio"]);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetLinesDocumentosHHOC, parameters);

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
                        Message = "Lista de documentos obtenidos correctamente.",
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
                    Message = "No fue posible obtener la lista de documentos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string SolicitarAutorizacion()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.EntradaMercanciaByOC RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.EntradaMercanciaByOC));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.EntradaMercanciaByOC)serializer.Deserialize(stringReader);
                    }
                }


                //Registrar la solicitud de autorización en la tabla de documentos ya que de ahi partira despues de que se autorice
                parameters.Add("OrdenCompra", RequestData.OrdenCompra);
                parameters.Add("SapDocument", RequestData.SapDocument);
                parameters.Add("Estatus", "En autorizacion");
                parameters.Add("Autorizacion", "");
                //Generar folio nuevo para OC
                string folioEM = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCInsertaDocumentosHHOC, parameters);

                string result;
                if (folioEM == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible enviar la solicitud de autorización. Por favor intenta de nuevo más tarde",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (folioEM.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible enviar la solicitud de autorización: " + folioEM,
                        Data = new List<Dictionary<string, object>>()
                    };
                }

                else
                {

                    JArray Folio = JArray.Parse(folioEM);

                    folioEM = Folio[0]["Folio"].ToString();

                    string baseUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}{Url.Content("~")}";
                    string encryptedFolio = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(folioEM)));
                    string encryptedSI = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes("SI")));
                    string encryptedNO = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes("NO")));
                    string aceptarUrl = $"{baseUrl}/Compras/AutorizarSolicitudHHOC?Folio={encryptedFolio}&Autorizado={encryptedSI}";
                    string rechazarUrl = $"{baseUrl}/Compras/AutorizarSolicitudHHOC?Folio={encryptedFolio}&Autorizado={encryptedNO}";

                    //Solicitar la autorizacion
                    result = Logic.SolicitaAutorizacion(RequestData.OrdenCompra, RequestData.SapDocument, RequestData.Mensaje, aceptarUrl, rechazarUrl);

                    if (result == "[]")
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {

                            Status = "NO",
                            Message = "No fue posible enviar la solicitud de autorización. Por favor intenta de nuevo más tarde",
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else if (result.Contains("Error"))
                    {
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {

                            Status = "ERROR",
                            Message = "No fue posible enviar la solicitud de autorización: " + result,
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
                            Message = "Solicitud procesada correctamente.",
                            Data = dataList
                        };
                    }
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
                    Message = "No fue posible enviar la solicitud de autorización: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string RevisarAutorizacionHHOC()
        {
            AccesoDatos.JsonResponse jsonResponse;
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            try
            {

                //Obtener estatus de autorizacion
                parameters.Add("OrdenCompra", Request.Headers["OrdenCompra"]);
                parameters.Add("SapDocument", Request.Headers["SapDocument"]);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCRevisarAutorizacionHHOC, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible consultar el estado de autorización, no se encontró información asociada.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible consultar el estado de autorización: " + result,
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
                        Message = "Solicitud procesada correctamente.",
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
                    Message = "No fue posible consultar el estado de autorización: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public ActionResult AutorizarSolicitudHHOC()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            string FolioEmail = string.Empty;
            try
            {

                //ANTES DE CONTINUAR CON LA SOLICITUD, VALIDAR SI YA HA SIDO ATENDIDA
                string decodedFolio = Encoding.UTF8.GetString(Convert.FromBase64String(Request.QueryString["Folio"]));
                string decodedAutorizado = Encoding.UTF8.GetString(Convert.FromBase64String(Request.QueryString["Autorizado"]));
                FolioEmail = decodedFolio;
                string AutorizadoEmail = decodedAutorizado;

                parameters.Add("Folio", FolioEmail);
                string atendida = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetEstatusAutorizacionHHOC, parameters);
                JArray atendidaResult = JArray.Parse(atendida);
                string result = atendidaResult[0]["Autorizacion"].ToString();
                StringBuilder Mensaje = new StringBuilder();
                string OC = string.Empty;
                string Estatus = string.Empty;
                string IconResult = string.Empty;
                switch (result)
                {
                    case "":
                        //Atender solicitud
                        parameters.Clear();
                        parameters.Add("Folio", FolioEmail);
                        parameters.Add("Autorizado", AutorizadoEmail);
                        parameters.Add("Estatus", (AutorizadoEmail == "SI" ? "Revisado" : "No autorizado"));
                        result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCAutorizarSolicitudHHOC, parameters);

                        Mensaje = new StringBuilder();
                        OC = FolioEmail.Split('-').ElementAt(0);
                        Estatus = (AutorizadoEmail == "SI" ? "Autorizada" : "Rechazada");
                        IconResult = (AutorizadoEmail == "SI" ? $@"<i class=""bi bi-check-circle-fill icon-ok""></i>" : $@"<i class=""bi bi-x-circle-fill icon-no""></i>");
                        Mensaje.Append("La solicitud de autorización para la orden de compra " + FolioEmail + " ha sido: " + Estatus + ".");

                        //Enviar notificacion al usuario de el estatus de su solicitud;
                        Logic.AvisoAutorizacion(FolioEmail.Split('-').ElementAt(0), Mensaje.ToString() + " Puedes validar tu solicitud en la HAND HELD.");
                        ViewBag.Mensaje = Mensaje.ToString();
                        ViewBag.IconResult = IconResult;
                        break;

                    default:
                        Mensaje = new StringBuilder();
                        OC = FolioEmail.Split('-').ElementAt(0);
                        Estatus = (result == "SI" ? "Autorizada" : "Rechazada");
                        IconResult = (result == "SI" ? $@"<i class=""bi bi-check-circle-fill icon-ok""></i>" : $@"<i class=""bi bi-x-circle-fill icon-no""></i>");
                        Mensaje.Append("La solicitud de autorización para la orden de compra " + FolioEmail + " ya ha sido: " + Estatus + ".");
                        ViewBag.Mensaje = Mensaje.ToString();
                        ViewBag.IconResult = IconResult;
                        break;
                }

                return View();

            }
            catch (Exception ex)
            {
                StringBuilder Error = new StringBuilder();
                string OC = FolioEmail.Split('-').ElementAt(0);
                Error.Append("No fue posible realizar la autorización de la OC: " + OC);
                Error.Append(ex.Message != null ? ex.Message.ToString() : string.Empty);
                Error.Append(ex.InnerException != null ? ex.InnerException.ToString() : string.Empty);
                ViewBag.Mensaje = Error.ToString();
                return View();
            }
        }

        [HttpGet]
        public string ValidarCantidadEMvsOC()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                string FolioOC = Request.Headers["FolioOC"];
                string OrdenCompra = Request.Headers["OrdenCompra"];
                string Articulo = Request.Headers["Articulo"];


                parameters.Add("FolioOC", FolioOC);
                parameters.Add("OrdenCompra", OrdenCompra);
                parameters.Add("Articulo", Articulo);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarCantidadEMvsOC, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener las cantidades de los escaneos. no se encontró información asociada al código.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }

                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener las cantidades de los escaneos: " + result,
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
                        Message = "Proceso completado correctamente.",
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
                    Message = "No fue posible obtener las cantidades de los escaneos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
        [HttpGet]
        public string ValidarExcesoCantidadEMvsOC()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                string OrdenCompra = Request.Headers["OrdenCompra"];
                string FolioOC = Request.Headers["FolioOC"];


                parameters.Add("OrdenCompra", OrdenCompra);
                parameters.Add("FolioOC", FolioOC);

                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarExcesoCantidadEMvsOC, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener las cantidades de los escaneos. no se encontró información asociada al código.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener las cantidades de los escaneos: " + result,
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
                        Message = "Proceso completado correctamente.",
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
                    Message = "No fue posible obtener las cantidades de los escaneos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string Requierekilos()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                string Articulo = Request.Headers["Articulo"];
                parameters.Add("Articulo", Articulo);


                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCRequierekilos, parameters);

                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener el rango en kilos. no se encontró información asociada al artículo.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }

                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener el rango en kilos: " + result,
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
                        Message = "Proceso completado correctamente.",
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
                    Message = "No fue posible obtener el rango en kilos: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
    }
}