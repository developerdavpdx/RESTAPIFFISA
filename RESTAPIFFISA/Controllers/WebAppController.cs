using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace RESTAPIFFISA.Controllers
{
    public class WebAppController : Controller
    {

        readonly LogicaWeb Logic = new LogicaWeb();

        [HttpPost]
        public async Task<string> GenerarOF(string PlanProduccion)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            try
            {
                parameters.Clear();
                parameters.Add("PlanProduccion", PlanProduccion);
                string OVP = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetOrdenesFabricacionPendientes, parameters);
                JArray OF = JArray.Parse(OVP);
                // 🔐 Intenta iniciar sesión con Service Layer
                var login = await Logic.LoginService.LoginAsyncHttpClient();

                string result;
                if (login.IsError)
                {
                    // Si el login falla, se incluye el mensaje de error detallado en la respuesta
                    result = $"Error: Login SAP fallido: {login.Message}";

                    return result;
                }

                foreach (JObject item in OF)
                {
                    string json = JsonConvert.SerializeObject(item);

                    parameters = await Logic.GenerarOF(item);

                    Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCUpdatePlanProduccionDetails, parameters);

                    parameters.Clear();
                }

                await Logic.LoginService.LogoutAsyncHttpClient();

                result = "OK";
                return result;
            }
            catch (Exception ex)
            {
                //Devolver el error en formato JSON
                string MethodName = MethodBase.GetCurrentMethod().Name;
                string ControllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
                string msg = "No es posible generar las ordenes de fabricación " + MethodName + " en: " + ControllerName + ", por favor contacte al administrador del sistema con el siguiente código de error: ";
                string finalmessage = Logic.GlobalCommands.Excepcion(ex, msg).ToString();
                return finalmessage;
            }
        }
    }
}