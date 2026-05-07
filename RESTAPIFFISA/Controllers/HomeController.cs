using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Mvc;
using System.Xml.Serialization;

namespace RESTAPIFFISA.Controllers
{
    public class HomeController : Controller
    {

        readonly Logica Logic = new Logica();
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public string ValidaUsuario()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            AccesoDatos.Credenciales RequestData;

            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.Credenciales));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.Credenciales)serializer.Deserialize(stringReader);

                    }
                }

                parameters.Add("email", RequestData.Usuario);
                parameters.Add("password", RequestData.Password);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidaUsuario, parameters);
                if (result == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible iniciar sesión, valida tus credenciales.",
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
                        Message = "Credenciales validadas correctamente.",
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
                    Message = "No fue posible iniciar sesión: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string FechaActiva()
        {
            _ = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {
                DateTime FechaVigencia = DateTime.Parse(ConfigurationManager.AppSettings["FechaVigencia"]);
                DateTime Hoy = DateTime.Now.Date;
                string Go = (FechaVigencia > Hoy ? "TRUE" : "FALSE");
                string result = "[{\"Continuar\": \"" + Go + "\"}]";

                // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "OK",
                    Message = "Fecha de activación obtenida correctamente.",
                    Data = dataList
                };
                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);

                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener la fecha de activación: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

    }
}