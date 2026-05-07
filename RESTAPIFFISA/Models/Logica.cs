using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RESTAPIFFISA
{
    public class Logica
    {
        public AccesoDatos AD = new AccesoDatos();
        public LoginServiceLayer LoginService = new LoginServiceLayer();
        public GlobalCommands GlobalCommands = new GlobalCommands();
        private static readonly ILog log = LogManager.GetLogger(typeof(Logica));

        public string Error { get; set; }
        public string User
        {
            get
            {
                return ConfigurationManager.AppSettings["User"]?.ToString() ?? string.Empty;
            }
        }
        public string Pass
        {
            get
            {
                return ConfigurationManager.AppSettings["Pass"]?.ToString() ?? string.Empty;
            }
        }
        public string EmailHost
        {
            get
            {
                return ConfigurationManager.AppSettings["EmailHost"]?.ToString() ?? string.Empty;
            }
        }
        public string EmailPort
        {
            get
            {
                return ConfigurationManager.AppSettings["EmailPort"]?.ToString() ?? string.Empty;
            }
        }
        public string EmailFrom
        {
            get
            {
                return ConfigurationManager.AppSettings["EmailFrom"]?.ToString() ?? string.Empty;
            }
        }
        public string EmailPassword
        {
            get
            {
                return ConfigurationManager.AppSettings["EmailPassword"]?.ToString() ?? string.Empty;
            }
        }


        public JavaScriptSerializer jsSerializer;

        #region Compras
        public string CreateEMDirecta(string Folio)
        {
            string AE = string.Empty;
            string LA = string.Empty;
            Company oCompany = new Company();
            Recordset oRecordSet = null;

            try
            {
                // Conectar a SAP Business One
                oCompany.Server = ConfigurationManager.AppSettings["SapServer"];
                oCompany.DbServerType = BoDataServerTypes.dst_MSSQL2019;
                oCompany.CompanyDB = ConfigurationManager.AppSettings["SapDatabase"];
                oCompany.UserName = ConfigurationManager.AppSettings["SapUser"];
                oCompany.Password = ConfigurationManager.AppSettings["SapPassword"];
                oCompany.language = BoSuppLangs.ln_Spanish_La;
                oCompany.UseTrusted = false;

                int lRetCode = oCompany.Connect();
                if (lRetCode != 0)
                {
                    return "[{\"Message\": \"Error de conexión a SAP: " + oCompany.GetLastErrorDescription().Replace("\"", "\\\"") + "\"}]";
                }

                Console.WriteLine("Conectado a SAP Business One");

                // Listado de documentos escaneados agrupados por artículo
                Dictionary<string, string> parameters = new Dictionary<string, string>
                {
                    { "Folio", Folio }
                };

                AE = GlobalCommands.ExecuteProcedure(AD.GCGetDetailsDocumentosHHEMD, parameters);
                JArray ArticulosEscaneos = JArray.Parse(AE);

                // Crear la Entrada de Mercancía
                Documents oGoodsReceipt = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oInventoryGenEntry);
                oGoodsReceipt.DocDate = DateTime.Now;
                oGoodsReceipt.DocDueDate = DateTime.Now;
                oGoodsReceipt.Comments = "Entrada de Mercancía Directa generada desde Interfaz FFISA";

                // Agregar las líneas del artículo
                foreach (JObject articulo in ArticulosEscaneos)
                {
                    oGoodsReceipt.Lines.ItemCode = articulo["Articulo"].ToString();
                    oGoodsReceipt.Lines.Quantity = Convert.ToDouble(articulo["Cantidad"].ToString());
                    oGoodsReceipt.Lines.WarehouseCode = articulo["Almacen"].ToString(); // Verifica que este dato venga en la consulta.

                    // Consultar si el artículo maneja lotes
                    oRecordSet = (Recordset)oCompany.GetBusinessObject(BoObjectTypes.BoRecordset);
                    string query = $"SELECT ManBtchNum FROM OITM WHERE ItemCode = '{articulo["Articulo"].ToString()}'";
                    oRecordSet.DoQuery(query);
                    bool isBatchManaged = oRecordSet.Fields.Item("ManBtchNum").Value.ToString() == "Y";
                    int howmanybatch = 0;

                    if (isBatchManaged)
                    {
                        parameters.Clear();
                        parameters.Add("Folio", Folio);
                        parameters.Add("Articulo", articulo["Articulo"].ToString());
                        LA = GlobalCommands.ExecuteProcedure(AD.GCGetLotesDocumentosHHEMD, parameters);
                        JArray LotesArticulos = JArray.Parse(LA);
                        howmanybatch = LotesArticulos.Count;

                        foreach (JObject lotes in LotesArticulos)
                        {
                            oGoodsReceipt.Lines.BatchNumbers.BatchNumber = lotes["Lote"].ToString();
                            oGoodsReceipt.Lines.BatchNumbers.Quantity = Convert.ToDouble(lotes["Cantidad"].ToString());
                            oGoodsReceipt.Lines.BatchNumbers.Add();
                        }
                    }

                    // Agregar campo de usuario con cantidad de lotes
                    oGoodsReceipt.Lines.UserFields.Fields.Item("U_norollos").Value = howmanybatch.ToString();

                    oGoodsReceipt.Lines.Add();
                }

                // Intentar agregar la Entrada de Mercancía
                lRetCode = oGoodsReceipt.Add();
                if (lRetCode != 0)
                {
                    return "[{\"Message\": \"Error al agregar la Entrada de Mercancía: " + oCompany.GetLastErrorDescription().Replace("\"", "\\\"") + "\"}]";
                }

                // Obtener el número de documento generado
                string docEntry = oCompany.GetNewObjectKey();
                string docNumQuery = $"SELECT DocNum FROM OIGN WHERE DocEntry = {docEntry}";
                oRecordSet.DoQuery(docNumQuery);
                string EntradaMercancia = oRecordSet.EoF ? "" : oRecordSet.Fields.Item("DocNum").Value.ToString();

                // Actualizar estatus del documento
                parameters.Clear();
                parameters.Add("Folio", Folio);
                parameters.Add("EntradaMercancia", EntradaMercancia);
                GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosHHEMD, parameters);

                return "[{\"Message\": \"Éxito: Entrada de Mercancía creada. Documento: " + EntradaMercancia + "\", \"DocEntry\": \"" + docEntry + "\"}]";
            }
            catch (Exception ex)
            {
                return "[{\"Message\": \"Error inesperado: " + ex.Message.Replace("\"", "\\\"") + "\"}]";
            }
            finally
            {
                if (oCompany.Connected)
                {
                    oCompany.Disconnect();
                }
            }
        }

        public string CreateEMFromOC(string Folio, string OrdenCompra) //Entrada de mercancia a partir de OC
        {
            string AE = string.Empty;
            string LA = string.Empty;
            Company oCompany = new Company();
            Recordset oRecordSet = null;

            try
            {
                // Conectar a SAP Business One
                oCompany.Server = ConfigurationManager.AppSettings["SapServer"];
                oCompany.DbServerType = BoDataServerTypes.dst_MSSQL2019;
                oCompany.CompanyDB = ConfigurationManager.AppSettings["SapDatabase"];
                oCompany.UserName = ConfigurationManager.AppSettings["SapUser"];
                oCompany.Password = ConfigurationManager.AppSettings["SapPassword"];
                oCompany.language = BoSuppLangs.ln_Spanish_La;
                oCompany.UseTrusted = false;

                int lRetCode = oCompany.Connect();
                if (lRetCode != 0)
                {
                    return "[{\"Message\": \"Error de conexión a SAP: " + oCompany.GetLastErrorDescription().Replace("\"", "\\\"") + "\"}]";
                }

                Console.WriteLine("Conectado a SAP Business One");

                //Listado de documentos escaneados agrupados por articulo
                Dictionary<string, string> parameters = new Dictionary<string, string>
                {
                    { "Folio", Folio }
                };
                AE = GlobalCommands.ExecuteProcedure(AD.GCGetDetailsDocumentosHHOC, parameters);

                if (AE.Contains("Error"))
                {
                    return "[{\"Message\": \"Error: La Orden de Compra #" + AE + ".\"}]";
                }

                JArray ArticulosEscaneos = JArray.Parse(AE);

                // Obtener la Orden de Compra
                Documents oPurchaseOrder = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oPurchaseOrders);
                if (!oPurchaseOrder.GetByKey(Convert.ToInt32(OrdenCompra)))
                {
                    return "[{\"Message\": \"Error: La Orden de Compra #" + OrdenCompra + " no existe en SAP.\"}]";
                }

                // Crear la Entrada de Mercancía
                Documents oGoodsReceiptPO = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oPurchaseDeliveryNotes);
                oGoodsReceiptPO.DocDate = DateTime.Now;
                oGoodsReceiptPO.DocDueDate = DateTime.Now;
                oGoodsReceiptPO.Comments = "Entrada de Mercancía generada desde Interfaz FFISA para la OC: " + ArticulosEscaneos[0]["SapDocument"];
                oGoodsReceiptPO.CardCode = oPurchaseOrder.CardCode;

                // Agregar las líneas del artículo en la OC
                //Articulos se deben agrupar por ItemCode para que de esta manera
                //Agregue el articulo y su lista de lotes

                foreach (JObject articulo in ArticulosEscaneos)
                {
                    oGoodsReceiptPO.Lines.BaseType = 22; // Tipo de documento base (Orden de Compra)
                    oGoodsReceiptPO.Lines.BaseEntry = Convert.ToInt32(OrdenCompra);
                    oGoodsReceiptPO.Lines.BaseLine = Convert.ToInt32(articulo["Linea"].ToString());
                    oGoodsReceiptPO.Lines.ItemCode = articulo["Articulo"].ToString();
                    oGoodsReceiptPO.Lines.Quantity = Convert.ToDouble(articulo["Cantidad"].ToString());
                    oGoodsReceiptPO.Lines.WarehouseCode = oPurchaseOrder.Lines.WarehouseCode;


                    // Consultar si el artículo maneja lotes
                    oRecordSet = (Recordset)oCompany.GetBusinessObject(BoObjectTypes.BoRecordset);
                    string query = $"SELECT ManBtchNum FROM OITM WHERE ItemCode = '{articulo["Articulo"].ToString()}'";
                    oRecordSet.DoQuery(query);
                    bool isBatchManaged = oRecordSet.Fields.Item("ManBtchNum").Value.ToString() == "Y";
                    int howmanybatch = 0;
                    if (isBatchManaged)
                    {
                        parameters.Clear();
                        parameters.Add("Folio", Folio);
                        parameters.Add("Articulo", articulo["Articulo"].ToString());
                        LA = GlobalCommands.ExecuteProcedure(AD.GCGetLotesDocumentosHHOC, parameters);
                        JArray LotesArticulos = JArray.Parse(LA);
                        howmanybatch = LotesArticulos.Count;
                        // Si el artículo usa lotes, agregar múltiples lotes si existen en `Escaneos`
                        foreach (JObject lotes in LotesArticulos)
                        {
                            oGoodsReceiptPO.Lines.BatchNumbers.BatchNumber = lotes["Lote"].ToString();
                            oGoodsReceiptPO.Lines.BatchNumbers.Quantity = Convert.ToDouble(lotes["Cantidad"].ToString());
                            oGoodsReceiptPO.Lines.BatchNumbers.Add();
                        }
                    }
                    // Agregar el campo de usuario a la línea actual
                    oGoodsReceiptPO.Lines.UserFields.Fields.Item("U_norollos").Value = howmanybatch.ToString();
                    //Colocar la cantidad de piezas escaneadas
                    oGoodsReceiptPO.Lines.WarehouseCode = oPurchaseOrder.Lines.WarehouseCode;

                    oGoodsReceiptPO.Lines.Add(); // Agregar línea de artículo
                }



                // Intentar agregar la Entrada de Mercancía
                lRetCode = oGoodsReceiptPO.Add();
                if (lRetCode != 0)
                {
                    return "[{\"Message\": \"Error al agregar la Entrada de Mercancía: " + oCompany.GetLastErrorDescription().Replace("\"", "\\\"") + "\"}]";
                }

                // Obtener el número de documento generado
                string docEntry = oCompany.GetNewObjectKey();
                string docNumQuery = $"SELECT DocNum FROM OPDN WHERE DocEntry = {docEntry}";
                oRecordSet.DoQuery(docNumQuery);
                string EntradaMercancia = oRecordSet.EoF ? "" : oRecordSet.Fields.Item("DocNum").Value.ToString();

                //Actualizar estatus de documento antes de devolver estatus 
                parameters.Clear();
                parameters.Add("OrdenCompra", OrdenCompra);
                parameters.Add("Folio", Folio);
                parameters.Add("EntradaMercancia", EntradaMercancia);
                GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosHHOC, parameters);

                return "[{\"Message\": \"Éxito: Entrada de Mercancía creada. Documento: " + EntradaMercancia + "\", \"DocEntry\": \"" + docEntry + "\"}]";
            }
            catch (Exception ex)
            {
                return "[{\"Message\": \"Error inesperado: " + ex.Message.Replace("\"", "\\\"") + "\"}]";
            }
            finally
            {
                if (oCompany.Connected)
                {
                    oCompany.Disconnect();
                }
            }
        }

        public async Task<string> CrearEntradaMercanciaDesdeOC(string folio, string ordenCompra, string Fecha)
        {
            string serviceLayerUrl = ConfigurationManager.AppSettings["ServiceLayer"];
            string entradaUrl = $"{serviceLayerUrl}/PurchaseDeliveryNotes";

            try
            {
                // 1. Obtener datos de cabecera de la OC
                string ocUrl = $"{serviceLayerUrl}/PurchaseOrders({ordenCompra})";


                var ocResponse = await LoginService._httpClient.GetAsync(ocUrl);
                var ocContent = await ocResponse.Content.ReadAsStringAsync();

                if (!ocResponse.IsSuccessStatusCode)
                {
                    return $"[{{\"Message\": \"Error al consultar OC: {ocContent}\"}}]";
                }

                dynamic ocData = JsonConvert.DeserializeObject(ocContent);
                string cardCode = ocData.CardCode;

                // 2. Obtener líneas escaneadas por artículo
                var parametros = new Dictionary<string, string> { { "Folio", folio } };
                var articulosJson = GlobalCommands.ExecuteProcedure(AD.GCGetDetailsDocumentosHHOC, parametros);

                if (articulosJson.Contains("Error"))
                {
                    return $"[{{\"Message\": \"Error al obtener artículos: {articulosJson}\"}}]";
                }

                var articulosEscaneados = JArray.Parse(articulosJson);
                var lineasDocumento = new List<object>();

                foreach (JObject articulo in articulosEscaneados)
                {
                    string itemCode = articulo["Articulo"].ToString();
                    int baseLine = Convert.ToInt32(articulo["Linea"]);
                    double cantidad = Convert.ToDouble(articulo["Cantidad"]);
                    string whsCode = ocData.DocumentLines[baseLine].WarehouseCode;

                    // 3. Obtener lotes si aplica
                    parametros = new Dictionary<string, string>
                    {
                { "Folio", folio },
                { "Articulo", itemCode }
                };
                    var lotesJson = GlobalCommands.ExecuteProcedure(AD.GCGetLotesDocumentosHHOC, parametros);
                    var lotes = JArray.Parse(lotesJson);

                    var linea = new Dictionary<string, object>
                    {
                        ["BaseType"] = 22,
                        ["BaseEntry"] = Convert.ToInt32(ordenCompra),
                        ["BaseLine"] = baseLine,
                        ["ItemCode"] = itemCode,
                        ["Quantity"] = cantidad,
                        ["WarehouseCode"] = whsCode,
                        ["UserFields"] = new { U_norollos = lotes.Count }
                    };

                    if (lotes.Count > 0)
                    {
                        var batchNumbers = new List<object>();
                        foreach (var lote in lotes)
                        {
                            batchNumbers.Add(new
                            {
                                BatchNumber = lote["Lote"].ToString(),
                                Quantity = Convert.ToDouble(lote["Cantidad"])
                            });
                        }
                        linea["BatchNumbers"] = batchNumbers;
                    }

                    lineasDocumento.Add(linea);
                }
                DateTime FechaDocumento = DateTime.Parse(Fecha);
                // 4. Armar cuerpo del documento
                var payload = new
                {
                    CardCode = cardCode,
                    DocDate = FechaDocumento.ToString("yyyy-MM-dd"),
                    DocDueDate = FechaDocumento.ToString("yyyy-MM-dd"),
                    Comments = $"Entrada generada desde interfaz FFISA para la OC: {ordenCompra}",
                    DocumentLines = lineasDocumento
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 5. Enviar solicitud POST al Service Layer
                var response = await LoginService._httpClient.PostAsync(entradaUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic docCreated = JsonConvert.DeserializeObject(result);
                    string docNum = docCreated.DocNum;
                    string docEntry = docCreated.DocEntry;

                    // 6. Actualizar estatus en tu sistema
                    var updateParams = new Dictionary<string, string>
            {
                { "OrdenCompra", ordenCompra },
                { "Folio", folio },
                { "EntradaMercancia", docNum }
            };
                    GlobalCommands.ExecuteProcedure(AD.GCActualizaDocumentosHHOC, updateParams);

                    return $"[{{\"Message\": \"Éxito: Entrada de Mercancía creada. Documento: {docNum}\", \"DocEntry\": \"{docEntry}\"}}]";
                }
                else
                {
                    dynamic docCreated = JsonConvert.DeserializeObject(result);
                    int code = docCreated.error.code;
                    string message = docCreated.error.message.value;
                    return $"Error: {code} {message}";
                }
            }
            catch (Exception ex)
            {
                return $"Error al crear Entrada de Mercancía: {ex.Message.Replace("\"", "\\\"")}";
            }
        }

        #endregion

        #region global
        public string SolicitaAutorizacion(string OrdenCompra, string SapDocument, string Mensaje, string UrlBaseAceptar, string UrlBaseRechazar)
        {

            try
            {
                string articulos = string.Empty;
                //Obtener articulos de OC para enviar en el email
                Dictionary<string, string> RequestParameters = new Dictionary<string, string>
                {
                    { "OrdenCompra", OrdenCompra }
                };
                string OCLines = GlobalCommands.ExecuteProcedure(AD.GCGetLinesOrdenesCompra, RequestParameters);
                JArray Art = JArray.Parse(OCLines);
                foreach (JObject art in Art)
                {
                    articulos += $@"<tr>
                                    <td style=""width:100%"" class=""content"">
                                       <div class=""card"">
                                        <p><strong>📦 Artículo:</strong> {art["Descripcion"].ToString()}</p>
                                        <p><strong>📊 Cantidad:</strong> {art["Quantity"].ToString()}</p>
                                        <p><strong>💰 Monto Total:</strong> {art["LineTotal"].ToString()}</p>
                                        </div>
                                    </td>
                                   </tr>";
                }
                //Correo de confirmación de correo enviado
                string MessagePE = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Solicitud de Autorización</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 0;
            background-color: #f4f4f4;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            background-color: #354152;
            color: white;
            text-align: center;
            height: 90px;
        }}
        .header h1 {{
            margin: 0;
            padding: 15px 0;
        }}
        .content {{
            padding: 5px 20px 20px 20px;
            text-align: center;
        }}
        .info {{
            display: table;
            width: 100%;
            margin-bottom: 20px;
            table-layout: fixed;
        }}
        .info img {{
            max-width: 100%;
            height: auto;
        }}
        .info td {{
            padding: 10px;
            vertical-align: top;
        }}
        .footer {{
            background-color: #354152;
            color: #f0f1f2;
            text-align: center;
            padding: 10px;
            font-size: 12px;
        }}
        .footer a {{
            text-decoration: none;
            color: #f0f1f2;
        }}
        .button {{
            padding: 10px 20px;
            font-size: 16px;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            margin: 10px;
        }}
        .accept {{
            background-color: #8bbabb;
            color: white;
        }}
        .reject {{
            background-color: #c30e2e;
            color: white;
        }}
        .button-container {{
            margin-top: 20px; /* Se añadió espacio entre la tabla y los botones */
        }}
        .title{{
        color:#8bbabb;
        }}
        .descriptiontext{{
            font-size:16px;
            text-align:justify;
        }}
        .card {{
            background: white;
            width: 300px;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
            text-align: center;
            border: 1px solid black;
        }}

        .card img {{
            width: 100px;
            height: 100px;
            border-radius: 50%;
            margin-bottom: 10px;
        }}

        .card h2 {{
            margin: 10px 0 5px;
            font-size: 20px;
            color: #333;
        }}

        .card p {{
            margin: 5px 0;
            font-size: 14px;
            color: #777;
        }}

        .btn {{
            display: inline-block;
            margin-top: 10px;
            padding: 8px 15px;
            background-color: #3498db;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            font-size: 14px;
        }}

        .btn:hover {{
            background-color: #2980b9;
        }}

    </style>
</head>
<body>
    <table class=""container"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
        <tr>
            <td class=""header"" align=""center"">
                <img src=""https://fisfiber.com.mx/wp-content/uploads/2023/04/cropped-Logo_header-300x102-1.png"" style=""width: 180px;"" alt=""Activo"">
            </td>
        </tr>
        <tr>
            <td align=""center"">
                <h1 class=""title"">Solicitud de autorización OC: {SapDocument}</h1>
            </td>
        </tr>
        <tr>
            <td class=""content"">
                <!-- Nueva fila para el logo -->
                <table class=""info"" cellpadding=""0"" cellspacing=""0"">
                    <tr>
                        <td style=""width: 100%; text-align: center;"">
                            <p class=""descriptiontext"">{Mensaje}</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
        {articulos}
        <tr>
        <td class=""content"">
            <tr class=""content button-container"">
            <td>
                <table class=""info"" cellpadding=""0"" cellspacing=""0"">
                    <tr class=""content button-container"">
                        <td class=""content"" style=""width: 50%; text-align: right;"">
                            <a href=""{UrlBaseAceptar}"" class=""button accept"">Aceptar</a>
                        </td>
                        <td style=""width: 50%; text-align: left;"">
                             <a href=""{UrlBaseRechazar}"" class=""button reject"">Rechazar</a>
                        </td>
                    </tr>
                </table>
            </td>
          </tr>
        </td>
        </tr>
        <tr>
            <td class=""footer"">
                <p>Este es un correo generado automáticamente mediante interfáz FFISA.</p>
            </td>
        </tr>
    </table>
</body>
</html>";


                // Email el mensaje de correo
                var Email = new MailMessage
                {
                    From = new MailAddress(EmailFrom),
                    Subject = "Solicitud de autorización de orden de compra",
                    Body = MessagePE,
                    IsBodyHtml = true
                };


                //LA CONFIGURACION DEL SMTP SE PEUDE CAMBIAR EN EL WEB CONFIG
                var smtpMail = new SmtpClient
                {
                    Host = EmailHost,
                    Port = int.Parse(EmailPort),
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                     EmailFrom,
                     EmailPassword)
                };

                //Obtener destinatarios
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("Code", "Compras");
                string EmailsCompras = GlobalCommands.ExecuteProcedure(GlobalCommands.GCGetEmailAutorizacionesHHOC, parameters);
                JArray Emails = JArray.Parse(EmailsCompras);
                foreach (JObject email in Emails)
                {
                    // Agregar destinatario
                    Email.To.Add(email["Email"].ToString());
                }

                smtpMail.Send(Email);
                smtpMail.Dispose();
                Email.Dispose();

                return "[{\"Message\": \"Éxito: La solicitud de autorización fue enviada exitosamente, espera a recibir un email de confirmación cuando la solicitud sea autorizada. " + "\"}]"; ;

            }
            catch (Exception ex)
            {
                return "[{\"Message\": \"No fue posible solicitar autorización: " + ex.Message.Replace("\"", "\\\"") + "\"}]";
            }
        }

        public string AvisoAutorizacion(string SapDocument, string Mensaje)
        {

            try
            {
                //Correo de confirmación de correo enviado
                string MessagePE = $@"<!DOCTYPE html>
                            <html lang=""en"">
                            <head>
                                <meta charset=""UTF-8"">
                                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                <title>Solicitud de Autorización</title>
                                <style>
                                    body {{
                                        font-family: Arial, sans-serif;
                                        margin: 0;
                                        padding: 0;
                                        background-color: #f4f4f4;
                                    }}
                                    .container {{
                                        max-width: 600px;
                                        margin: 20px auto;
                                        background: #ffffff;
                                        border-radius: 8px;
                                        overflow: hidden;
                                        box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
                                    }}
                                    .header {{
                                        background-color: #354152;
                                        color: white;
                                        text-align: center;
                                        height: 90px;
                                    }}
                                    .header h1 {{
                                        margin: 0;
                                        padding: 15px 0;
                                    }}
                                    .content {{
                                        padding: 5px 20px 20px 20px;
                                        text-align: center;
                                    }}
                                    .info {{
                                        display: table;
                                        width: 100%;
                                        margin-bottom: 20px;
                                        table-layout: fixed;
                                    }}
                                    .info img {{
                                        max-width: 100%;
                                        height: auto;
                                    }}
                                    .info td {{
                                        padding: 10px;
                                        vertical-align: top;
                                    }}
                                    .footer {{
                                        background-color: #354152;
                                        color: #f0f1f2;
                                        text-align: center;
                                        padding: 10px;
                                        font-size: 12px;
                                    }}
                                    .footer a {{
                                        text-decoration: none;
                                        color: #f0f1f2;
                                    }}
                                    .button {{
                                        padding: 10px 20px;
                                        font-size: 16px;
                                        border: none;
                                        border-radius: 5px;
                                        cursor: pointer;
                                        margin: 10px;
                                    }}
                                    .accept {{
                                        background-color: #8bbabb;
                                        color: white;
                                    }}
                                    .reject {{
                                        background-color: #c30e2e;
                                        color: white;
                                    }}
                                    .button-container {{
                                        margin-top: 20px; /* Se añadió espacio entre la tabla y los botones */
                                    }}
                                    .title{{
                                    color:#8bbabb;
                                    }}
                                    .descriptiontext{{
                                        font-size:16px;
                                        text-align:justify;
                                    }}
                                    .card {{
                                        background: white;
                                        width: 300px;
                                        padding: 20px;
                                        border-radius: 10px;
                                        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
                                        text-align: center;
                                        border: 1px solid black;
                                    }}

                                    .card img {{
                                        width: 100px;
                                        height: 100px;
                                        border-radius: 50%;
                                        margin-bottom: 10px;
                                    }}

                                    .card h2 {{
                                        margin: 10px 0 5px;
                                        font-size: 20px;
                                        color: #333;
                                    }}

                                    .card p {{
                                        margin: 5px 0;
                                        font-size: 14px;
                                        color: #777;
                                    }}

                                    .btn {{
                                        display: inline-block;
                                        margin-top: 10px;
                                        padding: 8px 15px;
                                        background-color: #3498db;
                                        color: white;
                                        text-decoration: none;
                                        border-radius: 5px;
                                        font-size: 14px;
                                    }}

                                    .btn:hover {{
                                        background-color: #2980b9;
                                    }}

                                </style>
                            </head>
                            <body>
                                <table class=""container"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                                    <tr>
                                        <td class=""header"" align=""center"">
                                            <img src=""https://fisfiber.com.mx/wp-content/uploads/2023/04/cropped-Logo_header-300x102-1.png"" style=""width: 180px;"" alt=""Activo"">
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align=""center"">
                                            <h1 class=""title"">Solicitud de autorización OC: {SapDocument}</h1>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td class=""content"">
                                            <!-- Nueva fila para el logo -->
                                            <table class=""info"" cellpadding=""0"" cellspacing=""0"">
                                                <tr>
                                                    <td style=""width: 100%; text-align: center;"">
                                                        <p class=""descriptiontext"">{Mensaje}</p>
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                    </tr>
                                    </td>
                                    </tr>
                                    <tr>
                                        <td class=""footer"">
                                            <p>Este es un correo generado automáticamente mediante interfáz FFISA.</p>
                                        </td>
                                    </tr>
                                </table>
                            </body>
                            </html>";


                // Email el mensaje de correo
                var Email = new MailMessage
                {
                    From = new MailAddress(EmailFrom),
                    Subject = "Solicitud de autorización de orden de compra",
                    Body = MessagePE,
                    IsBodyHtml = true
                };

                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                //LA CONFIGURACION DEL SMTP SE PEUDE CAMBIAR EN EL WEB CONFIG
                var smtpMail = new SmtpClient
                {
                    Host = EmailHost,
                    Port = int.Parse(EmailPort),
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                     EmailFrom,
                     EmailPassword)
                };

                //Obtener destinatarios
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("Code", "Resp-Compras");
                string EmailsComprasSolicitud = GlobalCommands.ExecuteProcedure(GlobalCommands.GCGetEmailAutorizacionesHHOC, parameters);
                JArray Emails = JArray.Parse(EmailsComprasSolicitud);
                foreach (JObject email in Emails)
                {
                    // Agregar destinatario
                    Email.To.Add(email["Email"].ToString());
                }

                smtpMail.Send(Email);
                smtpMail.Dispose();
                Email.Dispose();

                return "[{\"Message\": \"Éxito: La solicitud de autorización fue enviada exitosamente. " + "\"}]"; ;

            }
            catch (Exception ex)
            {
                return "[{\"Message\": \"No fue posible solicitar autorización: " + ex.Message.Replace("\"", "\\\"") + "\"}]";
            }
        }
        #endregion

    }
}