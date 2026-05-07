using log4net;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RESTAPIFFISA
{
    public class LoginServiceLayer
    {
        #region variables
        private static readonly ILog log = LogManager.GetLogger(typeof(LoginServiceLayer));
        private readonly CookieContainer _cookieContainer = new CookieContainer();
        public readonly HttpClient _httpClient;
        private readonly HttpClientHandler _httpHandler;
        private string SessionId { get; set; }
        #endregion

        #region constructor
        public LoginServiceLayer() //se ejecuta al instanciar la clase
        {
            _httpHandler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(_httpHandler);
            _httpClient.Timeout = TimeSpan.FromSeconds(100); // Puedes ajustarlo
        }
        #endregion

        #region Login
        /// <summary>
        /// Realiza el login al Service Layer de SAP B1 utilizando HttpClient y almacena la cookie de sesión (B1SESSION).
        /// Requiere que la URL del Service Layer, base de datos, usuario y contraseña estén configurados en el App.config.
        /// </summary>
        /// <returns>Un objeto SapResponse que indica si el login fue exitoso y contiene el SessionId</returns>
        public async Task<GlobalCommands.SapResponse> LoginAsyncHttpClient()
        {
            // Se crea una respuesta por defecto, marcando error hasta que se valide lo contrario
            var responseAbx = new GlobalCommands.SapResponse { IsError = true };

            // Se construye la URL de login a partir de la configuración
            var loginUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Login";

            // Se arma el payload de la petición con los datos necesarios para iniciar sesión
            var loginPayload = new
            {
                CompanyDB = ConfigurationManager.AppSettings["SapDatabase"],   // Base de datos de SAP
                UserName = ConfigurationManager.AppSettings["SapUser"],        // Usuario de SAP
                Password = ConfigurationManager.AppSettings["SapPassword"],    // Contraseña de SAP
                Language = "23"
            };

            // Se serializa el objeto a JSON
            var jsonPayload = JsonConvert.SerializeObject(loginPayload);

            // Se configura el contenido que se enviará en la solicitud
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Se fuerza el charset como UTF-8 (requerido por algunos servidores SAP)
            content.Headers.ContentType.CharSet = "utf-8";

            try
            {
                // 🔄 Limpieza de encabezados previos para evitar duplicados
                _httpClient.DefaultRequestHeaders.Clear();

                // Se especifica que se espera una respuesta en formato JSON
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Encabezados específicos del Service Layer que aseguran compatibilidad
                _httpClient.DefaultRequestHeaders.Add("B1S-WCFCompatible", "true");
                _httpClient.DefaultRequestHeaders.Add("B1S-MetadataWithoutSession", "true");

                // Se desactiva "Expect: 100-continue" para evitar errores con algunos servidores
                _httpClient.DefaultRequestHeaders.ExpectContinue = false;

                // Se realiza la petición POST al endpoint de login
                var response = await _httpClient.PostAsync(loginUrl, content);

                // Se obtiene el contenido de la respuesta como texto
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Se deserializa la respuesta para obtener el SessionId
                    dynamic jsonResponse = JsonConvert.DeserializeObject(result);
                    string sessionId = jsonResponse.SessionId;
                    SessionId = sessionId;

                    // 🟢 Se almacena manualmente la cookie B1SESSION en el CookieContainer
                    var baseUri = new Uri(ConfigurationManager.AppSettings["ServiceLayer"]);
                    _cookieContainer.Add(baseUri, new Cookie("B1SESSION", sessionId));

                    // Se marca como login exitoso
                    responseAbx.IsError = false;
                    responseAbx.SessionId = sessionId;
                }
                else
                {
                    // SAP respondió con error, se registra el mensaje para depuración
                    responseAbx.Message = $"Login fallido ({(int)response.StatusCode}): {result}";
                }
            }
            catch (Exception ex)
            {
                // Se captura cualquier excepción de red o formato inesperado
                responseAbx.Message = $"Excepción: {ex.Message}";
            }

            // Se retorna el resultado final del intento de login
            return responseAbx;
        }
        /// <summary>
        /// Cierra la sesión activa del Service Layer de SAP B1 utilizando HttpClient.
        /// Utiliza las cookies previamente almacenadas en el _httpClient (especialmente B1SESSION y ROUTEID).
        /// </summary>
        /// <returns>
        /// Objeto SapResponse indicando si el logout fue exitoso o si ocurrió algún error.
        /// </returns>
        public async Task<GlobalCommands.SapResponse> LogoutAsyncHttpClient()
        {
            // Se crea una respuesta por defecto, marcando error hasta que se valide lo contrario
            var responseAbx = new GlobalCommands.SapResponse
            {
                IsError = true
            };

            // Construye la URL del endpoint de logout usando el Service Layer configurado
            var logoutUrl = $"{ConfigurationManager.AppSettings["ServiceLayer"]}/Logout";

            try
            {
                // Se prepara una solicitud HTTP POST al endpoint de logout
                var request = new HttpRequestMessage(HttpMethod.Post, logoutUrl);

                // NOTA: No es necesario establecer las cookies manualmente,
                // ya que el _httpClient está configurado con un CookieContainer persistente.

                // Limpia y configura los encabezados necesarios para la petición
                request.Headers.Accept.Clear(); // Limpia cualquier Accept anterior
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); // Esperamos JSON

                // Evita enviar el encabezado "Expect: 100-continue", que puede causar errores con SAP
                request.Headers.ExpectContinue = false;

                // Envía la solicitud de logout al Service Layer
                var response = await _httpClient.SendAsync(request);

                // Lee el cuerpo de la respuesta
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // ✅ Logout exitoso
                    responseAbx.IsError = false;
                    responseAbx.Message = "Logout exitoso.";
                }
                else
                {
                    // ❌ SAP respondió con error, se incluye el cuerpo de la respuesta para depuración
                    responseAbx.Message = $"Error al hacer logout: {result}";
                }
            }
            catch (Exception ex)
            {
                // Captura excepciones generales (red, formato, etc.)
                responseAbx.Message = $"Excepción en logout: {ex.Message}";
            }

            // Devuelve el resultado del intento de logout
            return responseAbx;
        }
        #endregion
    }
}