using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Mvc;
using System.Xml.Serialization;
using static RESTAPIFFISA.AccesoDatos;

namespace RESTAPIFFISA.Controllers
{
    public class ImpresionEtiquetasController : Controller
    {

        readonly Logica Logic = new Logica();


        //FUNCTIONS
        //Funcion para leer en JSON
        private Consecutivos LeerConsecutivos(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Si el archivo no existe, crea uno nuevo con valores iniciales
                var consecutivos = new Consecutivos
                {
                    Tubos = new ConsecutivoInfo { Consecutivo = 0, UltimaFechaReinicio = DateTime.Now },
                    Pacas = new ConsecutivoInfo { Consecutivo = 0, UltimaFechaReinicio = DateTime.Now }
                };
                System.IO.File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
                return consecutivos;
            }

            // Leer el archivo JSON
            string json = System.IO.File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Consecutivos>(json);
        }
        //Funcion para Escribir en JSON
        private void GuardarConsecutivos(string filePath, Consecutivos consecutivos)
        {
            // Guardar el archivo JSON
            System.IO.File.WriteAllText(filePath, JsonConvert.SerializeObject(consecutivos, Formatting.Indented));
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

        //Funcion para generar el codigo de barras
        private string GenerarCodigoBarras(string producto, string tipoEtiqueta)
        {
            try
            {
                //Ruta del archivo JSON
                string filePath = ConfigurationManager.AppSettings["ConsecutivosRuta"];
                //Número consecutivo
                ConsecutivoInfo NuevoConsecutivo = new ConsecutivoInfo();
                // Leer los consecutivos desde el archivo JSON
                var consecutivos = LeerConsecutivos(filePath);

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
                // Obtener la fecha actual
                DateTime fechaActual = DateTime.Now;

                // Formatear la fecha en el formato DDMMAA
                string fechaFormateada = fechaActual.ToString("ddMMyy");

                // Añadir la fecha formateada al código de barras
                codigoBarras += fechaFormateada;
                // Añadir el separador y el consecutivo
                codigoBarras += "_" + NuevoConsecutivo.Consecutivo.ToString();

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

        /////CONTROLLERS
        /// <summary>
        /// Creamos el zpl correspondiente para imprimir la etiqueta de pacas
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string Crear_ZPL_Pacas()
        {
            //JEMPLO DE XML
            //<? xml version = "1.0" encoding = "utf-8" ?>
            //             < Fecha_ingreso > 26 / 02 / 2024 </ Fecha_ingreso >
            //             < Producto > FSX76RELIETESTWCHT </ Producto >
            //             < Descripcion > ProductoTest </ Descripcion >
            //             < Kilos > 100 </ Kilos >
            //             </ ImpresionesPacas >
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            JsonResponse jsonResponse;
            ImpresionesPacas RequestData;
            string CodigoBarras = string.Empty;

            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(AccesoDatos.ImpresionesPacas));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (AccesoDatos.ImpresionesPacas)serializer.Deserialize(stringReader);
                    }
                }

                // Obtener parámetros
                string fecha = RequestData.Fecha_ingreso;
                string producto = RequestData.Producto;
                string descripcion = RequestData.Descripcion;
                string kilos = RequestData.Kilos;
                parameters.Add("Usuario", RequestData.Usuario);
                string ImpresorasRed = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetImpresorasSeleccionadas, parameters);
                parameters.Clear();

                if (ImpresorasRed.Contains("Error"))
                {

                    jsonResponse = new JsonResponse()
                    {
                        Status = "ERROR",
                        Message = ImpresorasRed,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    JArray Print = JArray.Parse(ImpresorasRed);
                    List<string> Impresoras = Print[0]["Impresoras"].ToString().Split(',').ToList();

                    // Validaciones
                    if (string.IsNullOrEmpty(fecha)) throw new ArgumentException("Fecha es requerida");
                    if (string.IsNullOrEmpty(producto)) throw new ArgumentException("Producto es requerido");
                    //if (string.IsNullOrEmpty(kilos)) throw new ArgumentException("Kilos son requeridos");
                    if (string.IsNullOrEmpty(descripcion)) throw new ArgumentException("descripcion es requerida");

                    // Generar el código de barras
                    CodigoBarras = GenerarCodigoBarras(producto, "pacas");

                    if (CodigoBarras.Contains("Error"))
                    {

                        jsonResponse = new JsonResponse()
                        {
                            Status = "ERROR",
                            Message = CodigoBarras,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        // Construir ZPL
                        StringBuilder zpl = new StringBuilder();
                        zpl.AppendLine("^XA^JUS^XZ"); // Cancelar ajustes de la impresora (para evitar que se afecte el zpl)
                        zpl.AppendLine("^XA"); // Inicio del formato
                        zpl.AppendLine("^CI28"); // Habilitar UTF-8 (para imprimir correctamente caracteres especiales)
                        zpl.AppendLine("^CF0,30"); // Fuente principal

                        //Imagen del logo (codigo de la imagen transformado a GRF para que lo soporte ZPL, tambien se podria realizar la conversion con codigo)
                        zpl.AppendLine("^FO420,30^GFA,5382,5382,46,,::::R03IFE,Q03LF,P03NF,O01OFE,O07PF8,N01QFE,N07RF8,M01SFE,M07TF,M0UFC,L03UFE,L07VF8,L0WFC,K01WFEhK01FC,K03F8S03FFhJ07IFE,K0FFU0FF8P0gHF801FFCJ03KFCO07FFC,K0FFU0FFCO03gHFC03FFCJ0MFO0IFE,J01FFU0FFEO03gHFC03FFCI03MF8N0IFE,J03FFU0IFO03gHF803FFCI07MFCM01IFE,J07FFT01IF8N03gHF803FFC001NFEM03JF,J0FFET01IF8N03gHF803FFC003OFM03JF,I01FFET01IFCN07gHF807FF8007OFM07JF,I01FFET01IFEN07gHF807FF800PF8L07JF,I03FFET03JFN07gHF007FF800JF01JF8L0KF,I07FFCT03JFN07gHF007FF801IF8003IFCL0KF,I07FFCT03JF8M0IFO07FFCO0IF003FFEJ0IFCK01KF,I0IFCT03JFCM0IFO03FF8O0IF003FFCJ07FFCK01KF,001IFCT03JFCM0FFEO03FF8O0IF007FF8J03FFCK03KF8,001IFCT07JFEM0FFEO07FF8O0IF007FFK03FFCK07KF8,003IF8T0KFEM0FFEO07FF8O0IF00IFK03FFCK07KF8,003IF8J0VFL01FFEO07FFO01FFE00FFEK03FFCK0IF7FF8,007IF8J0VFL01FFEO07FFO01FFE00FFEK03FFCK0FFE7FF8,007IF8I01VF8K01FFCO07FFO01FFE00FFEL081K01FFC3FF8,00JF8I01VF8K01FFCO0IFO01FFE00FFES03FFC3FF8,00JFJ01VF8K01FFCO0IFO01FFE01IFS03FF83FFC,00JFJ01VFCK03FFCO0FFEO03FFC01IF8R07FF83FFC,00JFJ01VFCK03FF8O0FFEO03FFC01IFCR07FF03FFC,01JFJ03VFEK03FF8O0FFEO03FFC01JF8Q0IF03FFC,01JFJ03VFEK03FF8N01FFEO03FFC01KFQ0FFE03FFC,01IFEJ03VFEK07FFCN01FFEO03FFC00LFO01FFE03FFC,03IFEJ03VFEK07NFE001OF8007FF800MFN03FFC03FFC,03IFEJ07WFK07NFE001OF8007FF800NFM03FFC01FFE,03IFEJ07WFK07NFE001OF8007FF8007MFEL07FF801FFE,07IFEJ07WFK07NFE003OFI07FF8003NF8K07FF001FFE,07IFCJ07WFK0OFC003OFI07FF8001NFEK0IF001FFE,07IFCJ0XFK0OFC003OFI0IFJ0OFK0IF001FFE,07IFCJ0XF8J0OFC003OFI0IFJ03NF8I01FFE001FFE,07IFCJ07WF8J0OFC007OFI0IFK0NF8I03FFC001FFE,07IFCR01OF8J0OFC007NFEI0IFL0MFCI03FFC001IF,07IF8S0OF8I01IFO07FFCN01FFEM0LFCI07FF8001IF,07IF8S0OF8I01FFEO07FFO01FFEN0KFEI07FF8001IF,07IF8R01OF8I01FFCO0IFO01FFEO0JFEI0IF8001IF,07IF8R01OF8I01FFCO0IFO01FFEO01IFEI0IF8003IF,07IF8R01OF8I03FFCO0IFO01FFEP0IFE001PF,07IFS01OF8I03FFCO0FFEO03FFCP03FFE003PF,07IFS03OF8I03FFCO0FFEO03FFCP03FFE003PF8,07IFS03OF8I03FF8O0FFEO03FFCP01FFE007PF8,07IFS03OF8I03FF8N01FFEO03FFC03FF8K01FFC007PF8,07IFS03OF8I07FF8N01FFEO03FFC07FF8K01FFC00QF8,07FFES03OF8I07FF8N01FFCO07FF807FF8K03FFC01QF8,07FFES07OF8I07FFO01FFCO07FF807FF8K03FFC01QF8,07FFES07OF8I07FFO03FFCO07FF807FFCK07FF803QF8,07FFES0PFJ07FFO03FFCO07FF807FFCK07FF803MF1IFC,07FFCJ07XFJ0IFO03FF8O0IF007FFEK0IF007FFCK07FFC,03FFCJ0YFJ0IFO03FF8O0IF007IFJ03IF007FF8K07FFC,03FFCJ0YFJ0FFEO03FF8O0IF003IF8I0IFE00IF8K07FFC,03FFCJ0YFJ0FFEO07FF8O0IF003JF00JFC00IFL07FFC,03FFCJ0XFEJ0FFEO07FF8O0IF003PF801FFEL07FFC,01FF8J0XFEI01FFEO07FFO01FFE001PF003FFEL03FFC,01FF8I01XFEI01FFEO07FFO01FFEI0OFE003FFCL03FFE,01FF8I01XFCI01FFCO0IFO01FFEI0OFC007FFCL03FFE,00FF8I01XFCI01FFCO0IFO01FFEI03NF8007FF8L03FFE,00FF8I01XFCI03FFCO0IFO03FFEI01MFEI0IF8L03FFE,00FFJ01XF8I03FFCO0FFEO03FFCJ0MF8I0IFM03FFE,00FFJ01XF8I01FF8O0FFCO01FFCJ03KFEJ0FFEM01FFE,007FJ03XF8gW03IFE,007FJ03XF,003FJ03XF,001EJ03WFE,001EJ07WFE,I0EJ07WFC,I0EJ07WF8,I06J07WF8,I04J07WF,N07WF,N0WFE,N0WFC,N0WF8,:M01WF,M01VFE,M01VFC,M01VF8,M01VF,M03UFE,M03UFC,M03UF,M03TFE,M03TFC,M03TF,M01SFC,N07RF8,N01QFE,O07PF8,P0OFC,P03MFE,Q03LF,R01IFC,,:::^FS");

                        // Marco exterior
                        zpl.AppendLine("^FO14,15^GB775,620,7^FS");

                        // Fecha - Posición ajustada
                        zpl.AppendLine($"^FO30,30^FDFecha de ingreso: {fecha}^FS");

                        // Producto en 1 línea
                        zpl.AppendLine($"^FO30,70^FDProducto:^FS");
                        zpl.AppendLine($"^FO30,100^FB700,1,0,L,0^FD{producto}^FS");

                        // Descripción con 3 líneas de capacidad
                        zpl.AppendLine($"^FO30,190^FDDescripcion:^FS"); //<<--
                        zpl.AppendLine($"^FO30,220^A0N,30,27^FB700,3,0,L,0^FD{descripcion}^FS");

                        // Kilos (posición ajustada)
                        zpl.AppendLine($"^FO30,320^FDKilos: {kilos}^FS");

                        // Código de barras (posición ajustada)
                        zpl.AppendLine($"^FO40,400^BCN,100,Y,N,N^FD{CodigoBarras}^FS");

                        zpl.AppendLine("^XZ"); // Fin del formato

                        string zplCode = zpl.ToString();

                        //NO OLVIDAR DESCOMENTAR
                        foreach (string printer in Impresoras)
                        {
                            // Enviar a impresora
                            EnviarZPLConValidacion(printer, zplCode);
                        }

                        // Respuesta exitosa en XML
                        jsonResponse = new JsonResponse()
                        {
                            Status = "OK",
                            Message = CodigoBarras,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }
                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
            catch (Exception ex)
            {
                jsonResponse = new JsonResponse()
                {
                    Status = "ERROR",
                    Message = $"Error: {ex.Message}",
                    Data = new List<Dictionary<string, object>>()
                };

                //Si el consecutivo si fue generado y aun asi no se pudo imprimir la etiqueta 
                //Disminuir al consecutivo siempre y cuando no sea 0
                if (!CodigoBarras.Contains("Error"))
                {
                    //Ruta del archivo JSON
                    string filePath = ConfigurationManager.AppSettings["ConsecutivosRuta"];
                    //Número consecutivo
                    ConsecutivoInfo NuevoConsecutivo = new ConsecutivoInfo();
                    // Leer los consecutivos desde el archivo JSON
                    var consecutivos = LeerConsecutivos(filePath);
                    NuevoConsecutivo.Consecutivo = consecutivos.Pacas.Consecutivo;
                    NuevoConsecutivo.Consecutivo -= NuevoConsecutivo.Consecutivo;
                    // Guardar los cambios en el archivo JSON
                    consecutivos.Pacas.Consecutivo = NuevoConsecutivo.Consecutivo;
                    GuardarConsecutivos(filePath, consecutivos);
                }

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpPost]
        public string Crear_ZPL_Tubos()
        {
            //JEMPLO DE XML
            //<? xml version = "1.0" encoding = "utf-8" ?>
            //< ImpresionesTubos >
            //    < Producto > FSX76 RELIE TEST WCHT</ Producto >
            //</ ImpresionesTubos >
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            JsonResponse jsonResponse;
            ImpresionesTubos RequestData;
            string CodigoBarras = string.Empty;
            try
            {

                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(ImpresionesTubos));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (ImpresionesTubos)serializer.Deserialize(stringReader);
                    }
                }

                // Obtener y validar parámetro
                string producto = RequestData.Producto;
                string kilos = RequestData.Kilos;

                parameters.Add("Usuario", RequestData.Usuario);
                string ImpresorasRed = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetImpresorasSeleccionadas, parameters);
                parameters.Clear();

                if (ImpresorasRed.Contains("Error"))
                {

                    jsonResponse = new JsonResponse()
                    {
                        Status = "ERROR",
                        Message = ImpresorasRed,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    JArray Print = JArray.Parse(ImpresorasRed);
                    List<string> Impresoras = Print[0]["Impresoras"].ToString().Split(',').ToList();

                    if (string.IsNullOrEmpty(producto))
                        throw new ArgumentException("El campo producto es requerido");

                    // Generar el código de barras
                    CodigoBarras = GenerarCodigoBarras(producto, "tubos");

                    if (CodigoBarras.Contains("Error"))
                    {

                        jsonResponse = new JsonResponse()
                        {
                            Status = "ERROR",
                            Message = CodigoBarras,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        // Generar ZPL
                        StringBuilder zpl = new StringBuilder();
                        zpl.AppendLine("^XA^JUS^XZ"); // Cancelar ajustes de la impresora (para evitar que se afecte el zpl)
                        zpl.AppendLine("^XA");
                        zpl.AppendLine($"^FO320,30^A0N,40^FD{producto}^FS"); //Titulo de Producto
                        zpl.AppendLine($"^FO150,38^A0N,30^FDKilos {kilos} |^FS"); // Kilos
                        zpl.AppendLine($"^FO180,80^BCN,78,Y,N,N^FD{CodigoBarras}^FS"); //Codigo de barras
                        zpl.AppendLine("^XZ");
                        string zplCode = zpl.ToString();

                        //NO OLVIDAR DESCOMENTAR
                        foreach (string printer in Impresoras)
                        {
                            // Enviar a impresora
                            EnviarZPLConValidacion(printer, zplCode);
                        }

                        // Respuesta exitosa en XML
                        jsonResponse = new AccesoDatos.JsonResponse()
                        {
                            Status = "OK",
                            Message = CodigoBarras,
                            Data = new List<Dictionary<string, object>>()
                        };
                    }
                }
                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
            catch (Exception ex)
            {
                jsonResponse = new AccesoDatos.JsonResponse()
                {
                    Status = "ERROR",
                    Message = $"Error: {ex.Message}",
                    Data = new List<Dictionary<string, object>>()
                };
                //Si el consecutivo si fue generado y aun asi no se pudo imprimir la etiqueta 
                //Disminuir al consecutivo siempre y cuando no sea 0
                if (!CodigoBarras.Contains("Error"))
                {
                    //Ruta del archivo JSON
                    string filePath = ConfigurationManager.AppSettings["ConsecutivosRuta"];
                    //Número consecutivo
                    ConsecutivoInfo NuevoConsecutivo = new ConsecutivoInfo();
                    // Leer los consecutivos desde el archivo JSON
                    var consecutivos = LeerConsecutivos(filePath);
                    NuevoConsecutivo.Consecutivo = consecutivos.Tubos.Consecutivo;
                    NuevoConsecutivo.Consecutivo -= NuevoConsecutivo.Consecutivo;
                    // Guardar los cambios en el archivo JSON
                    consecutivos.Tubos.Consecutivo = NuevoConsecutivo.Consecutivo;
                    GuardarConsecutivos(filePath, consecutivos);
                }
                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        [HttpGet]
        public string GetImpresorasRed()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            JsonResponse jsonResponse;
            try
            {
                
                parameters.Add("Usuario", Request.Headers["Usuario"]);
                string result = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCGetImpresorasRed, parameters);
                if (result == "[]")
                {
                    jsonResponse = new JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible obtener la lista de impresoras. no se encontró información asociada.",
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else if (result.Contains("Error"))
                {
                    jsonResponse = new JsonResponse()
                    {

                        Status = "ERROR",
                        Message = "No fue posible obtener la lista de impresoras: " + result,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

                    jsonResponse = new JsonResponse()
                    {

                        Status = "OK",
                        Message = "Lista de impresoras obtenidas correctamente.",
                        Data = dataList
                    };
                }

                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                result = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return result;

            }
            catch (Exception ex)
            {
                jsonResponse = new JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible obtener la lista de impresoras: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
        }

        //Inserta el encabezado y lineas de la orden de compra para generar la EM
        [HttpPost]
        public string InsertaConfiguracionImpresion()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            JsonResponse jsonResponse;
            ConfiguracionImpresoras RequestData;
            try
            {
                // Leer el cuerpo de la solicitud
                using (StreamReader reader = new StreamReader(Request.InputStream))
                {
                    string xmlData = reader.ReadToEnd(); // Obtener el XML enviado
                    XmlSerializer serializer = new XmlSerializer(typeof(ConfiguracionImpresoras));

                    using (StringReader stringReader = new StringReader(xmlData))
                    {
                        //Deserializar los datos enviados en el modelo en este caso CREDENCIALES
                        RequestData = (ConfiguracionImpresoras)serializer.Deserialize(stringReader);
                    }
                }

                parameters.Add("Selected", RequestData.Selected);
                parameters.Add("Usuario", RequestData.Usuario);
                string UpdateImpresoraUsuario = Logic.GlobalCommands.ExecuteProcedure(Logic.AD.GCUpdateImpresoraEtiquetaHHEMD, parameters);

                if (UpdateImpresoraUsuario.Contains("Error"))
                {
                    jsonResponse = new JsonResponse()
                    {

                        Status = "NO",
                        Message = "No fue posible guardar la configuración de impresión,intentar de nuevo más tarde: " + UpdateImpresoraUsuario,
                        Data = new List<Dictionary<string, object>>()
                    };
                }
                else
                {
                    //Generar registro inicial de lineas
                    // 🔹 Convertir JSON a una lista de diccionarios (para manejar arrays)
                    List<Dictionary<string, object>> dataList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(UpdateImpresoraUsuario);

                    jsonResponse = new JsonResponse()
                    {
                        Status = "OK",
                        Message = "La configuración de impresión fue guardada correctamente.",
                        Data = dataList
                    };
                }


                // 🔹 Convertir el objeto `jsonResponse` en XML antes de devolverlo
                UpdateImpresoraUsuario = Logic.GlobalCommands.SerializeToXml(jsonResponse);
                return UpdateImpresoraUsuario;

            }
            catch (Exception ex)
            {
                jsonResponse = new JsonResponse()
                {

                    Status = "ERROR",
                    Message = "No fue posible guardar la configuración de impresión: " + ex.ToString(),
                    Data = new List<Dictionary<string, object>>()
                };

                return Logic.GlobalCommands.SerializeToXml(jsonResponse);
            }
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
    }
}