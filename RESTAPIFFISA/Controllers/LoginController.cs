using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Mvc;

namespace RESTAPIFFISA.Controllers
{
    public class LoginController : Controller
    {
        readonly Logica Logic = new Logica();

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public string ValidaUsuario()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;

            try
            {

                string Usuario = Request.Headers["Usuario"];
                string Password = Request.Headers["Password"];

                parameters.Add("email", Usuario);
                parameters.Add("password", Password);
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
                else if (result.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible iniciar sesión: " + result,
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
                        Message = ConfigurationManager.AppSettings["SapDatabase"].ToString(),
                        Data = dataList
                    };
                    //Validar si existe una sesión activa de lo contrario dejar avanzar
                    //parameters.Clear();
                    //parameters.Add("Email", Usuario);
                    //string SesionActiva = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCValidarSesionActiva, parameters);
                    //JArray SA = JArray.Parse(SesionActiva);
                    //if ((string)SA[0]["TieneSesionActiva"] == "0") //Si no hay una sesion activa
                    //{

                    //    //Registrar Sesion Activa
                    //    parameters.Clear();
                    //    parameters.Add("Email", Usuario);
                    //    string RegistrarSesion = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCRegistrarSesion, parameters);

                    //    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    //    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    //    jsonResponse = new AccesoDatos.JsonResponse()
                    //    {
                    //        Status = "OK",
                    //        Message = ConfigurationManager.AppSettings["SapDatabase"].ToString(),
                    //        Data = dataList
                    //    };
                    //}
                    //else
                    //{
                    //    jsonResponse = new AccesoDatos.JsonResponse()
                    //    {
                    //        Status = "NO",
                    //        Message = (string)SA[0]["Mensaje"],
                    //        Data = new List<Dictionary<string, object>>()
                    //    };
                    //}
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
        public string CerrarSesionActiva()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            AccesoDatos.JsonResponse jsonResponse;
            string result = string.Empty;

            try
            {
                string Usuario = Request.Headers["Usuario"];

                parameters.Add("Email", Usuario);
                string CerrarSesion = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCCerrarSesionActiva, parameters);
                if (CerrarSesion == "[]")
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "NO",
                        Message = "No fue posible cerrar la sesión activa, no se encontró sesión para cerrar.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (CerrarSesion.Contains("Error"))
                {
                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "ERROR",
                        Message = "No fue posible cerrar la sesión activa: " + CerrarSesion,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    JArray CSA = JArray.Parse(CerrarSesion);

                    jsonResponse = new AccesoDatos.JsonResponse()
                    {
                        Status = "OK",
                        Message = (string)CSA[0]["Mensaje"],
                        Data = new List<Dictionary<string, object>>()
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
                    Message = "No fue posible cerrar la sesión activa: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }
    }
}