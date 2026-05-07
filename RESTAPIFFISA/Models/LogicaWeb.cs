using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RESTAPIFFISA
{
    public class LogicaWeb
    {

        #region Variables
        public AccesoDatosWeb AD = new AccesoDatosWeb();
        public LoginServiceLayer LoginService = new LoginServiceLayer();
        public GlobalCommands GlobalCommands = new GlobalCommands();
        private static readonly ILog log = LogManager.GetLogger(typeof(LogicaWeb));

        #endregion

        #region SBO PLAN PRODUCCION

        /// <summary>
        /// Genera una orden de fabricación en SAP Business One mediante Service Layer,
        /// validando primero los datos de entrada y autenticándose.
        /// </summary>
        /// <param name="planProduccion">Objeto JObject con los datos requeridos para generar la orden</param>
        /// <returns>
        /// Diccionario con el estado del proceso (éxito o error) y datos como DocEntry, DocNum, etc.
        /// </returns>
        public async Task<Dictionary<string, string>> GenerarOF(JObject planProduccion)
        {
            // 📝 Log de inicio del proceso, útil para trazabilidad en producción
            //log.Info($"[GenerarOF] Inicio - Datos: {planProduccion}");

            // Se inicializa la estructura de respuesta con valores por defecto
            var response = InitializeResponse(planProduccion["id"].ToString());

            try
            {
                // ✅ Validación de campos requeridos antes de intentar comunicarse con SAP
                if (!ValidateRequiredFields(planProduccion, response))
                    return response; // Si falta algún campo, se retorna de inmediato con mensaje de error

                // 🏭 Si el login fue exitoso, se procede a crear la orden de fabricación
                response = await CreateProductionOrderSL(planProduccion);
            }
            catch (Exception ex)
            {
                // ⚠️ Captura cualquier excepción no controlada del flujo general y genera respuesta de error
                return HandleUncaughtException(ex, planProduccion["id"].ToString());
            }

            // ✅ Devuelve el resultado final del proceso (éxito o error detallado)
            return response;
        }


        /// <summary>
        /// Crea una orden de fabricación en SAP Business One a través del Service Layer,
        /// utilizando los datos proporcionados en un JObject (plan).
        /// </summary>
        /// <param name="plan">Objeto con los datos necesarios para generar la orden</param>
        /// <returns>Diccionario con la respuesta del proceso (estatus, docEntry, etc.)</returns>
        private async Task<Dictionary<string, string>> CreateProductionOrderSL(JObject plan)
        {
            // Inicializa el diccionario de respuesta con valores por defecto
            var response = InitializeResponse(plan["id"].ToString());

            // Construye la URL del endpoint del Service Layer para crear órdenes de fabricación
            var url = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/ProductionOrders";

            // Crea un objeto anónimo con la estructura esperada por el Service Layer
            var orden = new
            {
                ItemNo = plan["Articulo"].ToString(),                          // Código del artículo principal
                DueDate = DateTime.Today.ToString("yyyy-MM-dd"),              // Fecha compromiso (formato ISO)
                ProductionOrderType = "bopotStandard",                         // Tipo de orden: estándar
                PlannedQuantity = double.Parse(plan["CantidadKilos"].ToString()), // Cantidad planeada a fabricar
                Warehouse = plan["Almacen"].ToString(),                        // Código de almacén
                ProductionOrderOriginEntry = int.Parse(plan["DocEntry"].ToString()), // Referencia al documento origen
                Remarks = "Creado desde Service Layer"                    // Comentario libre
            };

            var json = JsonConvert.SerializeObject(orden);

            // Prepara el contenido de la solicitud HTTP
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                // Se realiza la solicitud POST al Service Layer con el JSON preparado
                var result = await LoginService._httpClient.PostAsync(url, content);

                // Se lee el contenido de la respuesta como texto
                var responseBody = await result.Content.ReadAsStringAsync();

                if (!result.IsSuccessStatusCode)
                {
                    // ❌ Si hubo error, se deserializa el cuerpo para obtener el mensaje de SAP
                    var jsonObj = JsonConvert.DeserializeObject<JObject>(responseBody);

                    // Se actualiza el estado con el mensaje de error proporcionado por SAP
                    response["StatusSap"] = $"Error SAP al crear OF: {jsonObj["error"]["message"]["value"]}";
                }
                else
                {
                    // ✅ Si la creación fue exitosa, se extraen los valores claves de la respuesta
                    var jsonObj = JsonConvert.DeserializeObject<JObject>(responseBody);

                    response["StatusSap"] = "Orden de fabricación creada correctamente.";
                    response["DocEntryOF"] = jsonObj["AbsoluteEntry"].ToString();   // DocEntry de la nueva orden
                    response["DocNumOF"] = jsonObj["DocumentNumber"].ToString();   // Número de documento
                    response["StatusProduccion"] = "2";                            // Estado actualizado (liberado)
                    response["Orden"] = null;
                }
            }
            catch (Exception ex)
            {
                // Captura errores generales (problemas de red, serialización, etc.)
                response["StatusSap"] = $"Excepción: {ex.Message}";
            }

            // Devuelve el resultado del intento de creación de la orden
            return response;
        }


        // Métodos auxiliares
        private Dictionary<string, string> InitializeResponse(string id)
        {
            return new Dictionary<string, string>
            {
                {"id", id},
                {"StatusSap", ""},
                {"DocNumOF", ""},
                {"DocEntryOF", ""},
                {"StatusProduccion", "1"},
                {"Orden", "-1"}
            };
        }

        private bool ValidateRequiredFields(JObject plan, Dictionary<string, string> response)
        {
            if (plan["CantidadKilos"] == null)
            {
                response["StatusSap"] = "No es posible generar la OF debido a que CantidadKilos está vacía.";
                return false;
            }

            if (!double.TryParse(plan["CantidadKilos"].ToString(), out _))
            {
                response["StatusSap"] = "No es posible generar la OF debido a que Cantidad Kilos contiene un formato incorrecto.";
                return false;
            }

            // Agregar más validaciones según sea necesario
            return true;
        }

        private Dictionary<string, string> HandleUncaughtException(Exception ex, string id)
        {
            string finalMessage = ex is TimeoutException ?
                "Timeout al conectar con SAP" :
                $"{ex.Message}";

            log.Fatal(finalMessage, ex);
            var response = InitializeResponse(id);
            response["StatusSap"] = finalMessage;
            return response;
        }
        #endregion

    }
}