using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RESTAPIFFISA
{
    public class GlobalCommands
    {
        #region global

        public string GCGetEmailAutorizacionesHHOC { get { return "EXEC SpPdxFF_GetEmailAutorizacionesHHOC @Code"; } }
        public string GCGetImpresorasSeleccionadas { get { return "SpPdxFF_GetImpresorasSeleccionadas @Usuario"; } }
        //Ejecutar query de resultado multiple en formato JSONSTRING
        public string ExecuteProcedure(string commandText, Dictionary<string, string> parameters)
        {
            string result = string.Empty;
            using (SqlConnection myConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["FFISAConnection"].ConnectionString))
            {
                try
                {
                    myConnection.Open();

                    using (SqlCommand cmd = new SqlCommand(commandText, myConnection))
                    {
                        cmd.CommandTimeout = 30;

                        if (parameters != null && parameters.Count > 0)
                        {
                            foreach (var parameter in parameters)
                            {
                                if (parameter.Value == null)
                                {
                                    cmd.Parameters.AddWithValue("@" + parameter.Key, DBNull.Value);
                                }
                                else
                                {
                                    cmd.Parameters.AddWithValue("@" + parameter.Key, parameter.Value);
                                }
                            }
                        }
                        cmd.CommandType = CommandType.Text;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            using (StringWriter sw = new StringWriter())
                            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw))
                            {
                                DataTable dtDocuments = new DataTable();
                                dtDocuments.Load(reader);

                                JsonSerializer serializer = new JsonSerializer();
                                serializer.Serialize(jsonWriter, dtDocuments);
                                result = sw.ToString();

                                //JsonSerializer serializer = new JsonSerializer
                                //{
                                //    // Configuraciones para escapar caracteres conflictivos y mejorar legibilidad
                                //    StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                                //    Formatting = Formatting.Indented // Cambiar a None si no necesitas el formato legible
                                //};
                            }
                        }
                    }
                }
                catch (Exception E)
                {
                    StringBuilder Error = new StringBuilder();
                    Error.Append("Error: ");
                    Error.Append(E.Message ?? "");
                    Error.Append(E.InnerException != null ? E.InnerException.ToString() : "");
                    result = Error.ToString();
                }
                finally
                {
                    if (myConnection.State == ConnectionState.Open)
                    {
                        myConnection.Close();
                    }
                }
            }

            return result;
        }
        public StringBuilder Excepcion(Exception E, string msg)
        {

            // 6. Obtener el número de línea del error
            int lineNumber = new StackTrace(E, true).GetFrame(0).GetFileLineNumber();

            // 7. Crear un mensaje de error detallado
            StringBuilder sb = new StringBuilder();
            sb.Append(msg);
            sb.Append(E.Message);
            sb.Append($" (Línea: {lineNumber})");

            return sb;
        }

        // 🔹 Método para convertir un objeto a XML
        public string SerializeToXml<T>(T obj)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringWriter textWriter = new StringWriter())
            {
                serializer.Serialize(textWriter, obj);
                return textWriter.ToString();
            }
        }

        // Método de login asíncrono con webrequest
        public async Task<GlobalCommands.SapResponse> LoginAsyncHttpWebRequest()
        {
            GlobalCommands.SapResponse responseAbx = new GlobalCommands.SapResponse();
            var loginUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"].ToString()}/Login";
            var loginPayload = new
            {
                CompanyDB = ConfigurationManager.AppSettings["SapDatabase"],     // Nombre de la base de datos en SAP
                UserName = ConfigurationManager.AppSettings["SapUser"],          // Usuario de SAP
                Password = ConfigurationManager.AppSettings["SapPassword"],         // Contraseña
                lang = "en-us"             // Idioma preferido de la sesión
            };
            responseAbx.IsError = true;
            // 1. Ignorar errores de SSL (solo para desarrollo)
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // Usar TLS 1.2

            var request = (HttpWebRequest)WebRequest.Create(loginUrl);
            request.Method = "POST";

            request.UseDefaultCredentials = true;
            request.ContentType = "application/json;odata=minimalmetadata;charset=utf8";

            request.KeepAlive = true;
            //     httpWebRequest.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            request.Accept = "application/json;odata=minimalmetadata";
            request.ServicePoint.Expect100Continue = false;
            request.Headers.Add("B1S-WCFCompatible", "true");
            request.Headers.Add("B1S-MetadataWithoutSession", "true");
            request.AllowAutoRedirect = true;
            request.Timeout = 10000000;

            using (var streamWriter = new StreamWriter(await request.GetRequestStreamAsync()))
            {
                var json = JsonConvert.SerializeObject(loginPayload);
                await streamWriter.WriteAsync(json);
                await streamWriter.FlushAsync();
            }

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        responseAbx.IsError = false;
                        var result = await reader.ReadToEndAsync();
                        dynamic jsonResponse = JsonConvert.DeserializeObject(result);
                        string SessionId = jsonResponse.SessionId;
                        responseAbx.SessionId = SessionId;
                        // return responseAbx;
                    }
                }
            }
            catch (WebException ex)
            {
                using (var errorResponse = (HttpWebResponse)ex.Response)
                {
                    using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorText = await reader.ReadToEndAsync();
                        Console.WriteLine($"Login failed: {errorText}");
                        responseAbx.IsError = true;
                        responseAbx.Message = errorText;

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");
                responseAbx.IsError = true;
                responseAbx.Message = ex.Message;
            }

            return responseAbx;
        }
        // Método cerrar sesion asíncrono con webrequest
        public async Task<GlobalCommands.SapResponse> LogoutAsyncHttpWebRequest(string SessionId)
        {
            var responseAbx = new GlobalCommands.SapResponse
            {
                IsError = true
            };

            var logoutUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Logout";
            var request = (HttpWebRequest)WebRequest.Create(logoutUrl);
            //request.Method = "POST";
            request.ContentType = "application/json";
            request.Method = "POST";
            request.KeepAlive = true;
            request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            request.Accept = "application/jsona";
            request.Headers.Add("Cookie", $"B1SESSION={SessionId}");


            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    responseAbx.IsError = false;
                    responseAbx.Message = "Logout exitoso.";
                }

                return responseAbx;
            }
            catch (Exception ex)
            {
                responseAbx.Message = $"Exception: {ex.Message}";
                return responseAbx;
            }
        }

        public Dictionary<string, string> ConvertToParameters(object obj)
        {
            var parameters = new Dictionary<string, string>();

            var properties = obj.GetType().GetProperties();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                // Convierte el valor a string o "" si es null
                parameters.Add(prop.Name, value?.ToString() ?? "");
            }

            return parameters;
        }
        /// <summary>
        /// Calcula el DiscountPercent exacto para que SAP genere un LineTotal igual al de la OV.
        /// </summary>
        /// <param name="unitPrice">Precio unitario del artículo (de la OV)</param>
        /// <param name="quantity">Cantidad que se va a entregar (en unidad nativa)</param>
        /// <param name="lineTotalOV">LineTotal de la OV que queremos replicar</param>
        /// <returns>DiscountPercent redondeado a 6 decimales, listo para enviar a Service Layer</returns>
        public decimal CalcularDiscountPercentExacto(decimal unitPrice, decimal quantity, decimal lineTotalOV)
        {
            // Validaciones
            if (unitPrice == 0 || quantity == 0)
            {
                Console.WriteLine("⚠️ unitPrice o quantity es 0 - retornando 0%");
                return 0;
            }

            // Calcular el porcentaje de descuento inicial
            decimal precioUnitarioEfectivo = lineTotalOV / quantity;
            decimal discountPercent = 100m * (1m - (precioUnitarioEfectivo / unitPrice));

            // Redondear a 6 decimales
            discountPercent = Math.Round(discountPercent, 6);

            // Simular cómo SAP va a calcular
            decimal precioConDescuento = unitPrice * (1m - discountPercent / 100m);
            precioConDescuento = Math.Round(precioConDescuento, 6); // SAP usa 6 decimales
            decimal lineTotalCalculado = Math.Round(precioConDescuento * quantity, 2); // SAP redondea a 2

            // Ajuste iterativo si hay diferencia
            decimal diferencia = lineTotalOV - lineTotalCalculado;
            int intentos = 0;

            while (Math.Abs(diferencia) > 0.01m && intentos < 10)
            {
                // Ajustar el descuento basado en la diferencia
                decimal ajuste = (diferencia / (unitPrice * quantity)) * 100m;
                discountPercent -= ajuste;
                discountPercent = Math.Round(discountPercent, 6);

                // Recalcular
                precioConDescuento = Math.Round(unitPrice * (1m - discountPercent / 100m), 6);
                lineTotalCalculado = Math.Round(precioConDescuento * quantity, 2);
                diferencia = lineTotalOV - lineTotalCalculado;

                intentos++;
            }

            // Debug
            Console.WriteLine($"💰 Descuento: {discountPercent:F6}% | Total OV: {lineTotalOV:F2} | Total Calculado: {lineTotalCalculado:F2} | Diferencia: ${diferencia:F4} | Intentos: {intentos}");

            // ⚠️ Opcional: Advertencia si no convergió bien
            if (Math.Abs(diferencia) > 0.02m)
            {
                Console.WriteLine($"⚠️ ADVERTENCIA: Diferencia mayor a 5 centavos después de {intentos} intentos");
            }

            return discountPercent;
        }
        #endregion

        #region Class
        public class SapResponse
        {
            public string SessionId { get; set; }
            public string Message { get; set; }
            // public string IdRole { get; set; }
            public object JsonRsp { get; set; }
            public string Version { get; set; }
            public bool IsError { get; set; }
            public string RouteId { get; set; }
            public string OrdenVenta { get; set; }
            public string RollosComprendidos { get; set; }

        }
        #endregion

    }
}