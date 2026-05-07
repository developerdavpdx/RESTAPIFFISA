using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RESTAPIFFISA
{
    public class AccesoDatos
    {
        #region GeneralCommands(Procedure declaration)

        #region Compras
        public string GCValidaUsuario { get { return "EXEC SpPdxFF_ValidaUsuario @email,@password "; } }
        public string GCValidarSesionActiva { get { return "EXEC SpPdxFFHHValidarSesionActiva @Email"; } }
        public string GCRegistrarSesion { get { return "EXEC SpPdxFFHHRegistrarSesion @Email"; } }
        public string GCCerrarSesionActiva { get { return "EXEC SpPdxFFHHCerrarSesionActiva @Email"; } }
        public string GCGetSeriesNumeraciondDocs { get { return "EXEC SpPdxFF_GetSeriesNumeraciondDocs @ObjectCode "; } }
        public string GCOrdenesCompra { get { return "EXEC SpPdxFF_GetOrdenesCompra @Busqueda,@FI,@FF,@Series,@OC"; } }
        public string GCGetDetailsOrdenesCompra { get { return "EXEC SpPdxFF_GetDetailsOrdenesCompra @DocEntry"; } }
        public string GCUpdateDetailsOrdenesCompra { get { return "EXEC SpPdxFF_UpdateDetailsOrdenesCompra @DocEntry,@CertificadoCalidad,@OrdenFisica,@PackingList,@Pedimento"; } }
        public string GCGetLinesOrdenesCompra { get { return "EXEC SpPdxFF_GetLinesOrdenesCompra @OrdenCompra"; } }
        public string GCInsertaDocumentosHHOC { get { return "EXEC SpPdxFF_InsertaDocumentosHHOC @OrdenCompra,@SapDocument,@Estatus,@Autorizacion"; } }
        public string GCEliminaDocumentosHHOC { get { return "EXEC SpPdxFF_EliminaDocumentosHHOC @Folio"; } }
        public string GCEliminaEscaneosHHOC { get { return "EXEC SpPdxFF_EliminaEscaneosHHOC @ID,@Folio"; } }
        public string GCInsertaDocumentosHHOCDetails { get { return "EXEC SpPdxFF_InsertaDocumentosHHOCDetails @Folio,@Articulo,@Linea,@Lote,@Cantidad"; } }
        public string GCGetDocumentosHHOC { get { return "EXEC SpPdxFF_GetDocumentosHHOC @FI,@FF"; } }
        public string GCGetDetailsDocumentosHHOC { get { return "EXEC SpPdxFF_GetDetailsDocumentosHHOC @Folio"; } }
        public string GCGetDetailsDocumentosHHEMD { get { return "EXEC SpPdxFF_GetDetailsDocumentosHHEMD @Folio"; } }
        public string GCGetLinesDocumentosHHOC { get { return "EXEC SpPdxFF_GetLinesDocumentosHHOC @Folio"; } }
        public string GCGetLotesDocumentosHHOC { get { return "EXEC SpPdxFF_GetLotesDocumentosHHOC @Folio,@Articulo"; } }
        public string GCGetLotesDocumentosHHEMD { get { return "EXEC SpPdxFF_GetLotesDocumentosHHEMD @Folio,@Articulo"; } }
        public string GCActualizaDocumentosHHOC { get { return "EXEC SpPdxFF_ActualizaDocumentosHHOC @OrdenCompra,@Folio,@EntradaMercancia"; } }
        public string GCActualizaDocumentosHHEMD { get { return "EXEC SpPdxFF_ActualizaDocumentosHHEMD @OrdenCompra,@Folio,@EntradaMercancia"; } }
        public string GCRevisarAutorizacionHHOC { get { return "EXEC SpPdxFF_RevisarAutorizacionHHOC @OrdenCompra,@SapDocument"; } }
        public string GCRequierekilos { get { return "EXEC SpPdxFF_Requierekilos @Articulo"; } }
        public string GCValidarCantidadEMvsOC { get { return "EXEC SpPdxFF_ValidarCantidadEMvsOC @FolioOC,@OrdenCompra,@Articulo"; } }
        public string GCValidarExcesoCantidadEMvsOC { get { return "EXEC SpPdxFF_ValidarExcesoCantidadEMvsOC @OrdenCompra,@FolioOC"; } }
        public string GCAutorizarSolicitudHHOC { get { return "EXEC SpPdxFF_AutorizarSolicitudHHOC @Folio,@Autorizado,@Estatus"; } }
        public string GCGetEstatusAutorizacionHHOC { get { return "SpPdxFF_GetEstatusAutorizacionHHOC @Folio"; } }
        public string GCGetImpresorasRed { get { return "SpPdxFF_GetImpresorasRed @Usuario"; } }
        public string GCGetImpresorasSeleccionadas { get { return "SpPdxFF_GetImpresorasSeleccionadas @Usuario"; } }
        public string GCUpdateImpresorasRed { get { return "SpPdxFF_UpdateImpresorasRed @Selected"; } }
        public string GCUpdateImpresoraEtiquetaHHEMD { get { return "SpPdxFF_UpdateImpresoraEtiquetaHHEMD @Selected,@Usuario"; } }

        #endregion

        #endregion

        #region SQLFunctions

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

        #endregion

        #region AditionalClassModel
        public Dictionary<string, string> RequestParameters { get; set; }

        // Definir la clase que representa los parámetros en JSON
        public class Credenciales
        {
            public string Usuario { get; set; }
            public string Password { get; set; }
        }
        public class OcCheckList
        {
            public string DocEntry { get; set; }
            public string CertificadoCalidad { get; set; }
            public string OrdenFisica { get; set; }
            public string PackingList { get; set; }
            public string Pedimento { get; set; }
        }
        public class EntradaMercanciaByOC
        {
            public string OrdenCompra { get; set; }
            public string SapDocument { get; set; }
            public string Articulo { get; set; }
            public string Linea { get; set; }
            public string Lote { get; set; }
            public string Cantidad { get; set; }
            public string Folio { get; set; }
            public string Mensaje { get; set; }
        }
        public class MultipleEntradaMercanciaByOC
        {
            public string OrdenCompra { get; set; }
            public string Folio { get; set; }
            public string Fecha { get; set; }
        }
        public class JsonResponse
        {
            public string Status { get; set; }
            public string Message { get; set; }

            [XmlIgnore] // No serializar directamente en XML
            public List<Dictionary<string, object>> Data { get; set; }

            [XmlElement("Data")]
            public string DataXml
            {
                get
                {
                    return ListToXml(Data);
                }
                set { }
            }

            private string ListToXml(List<Dictionary<string, object>> list)
            {
                if (list == null || list.Count == 0)
                    return "<List />"; // Si la lista está vacía, devolver un nodo raíz vacío

                var doc = new XmlDocument();
                var root = doc.CreateElement("List"); // Nodo raíz único
                doc.AppendChild(root);

                int count = 1;
                foreach (var dictionary in list)
                {
                    var itemElement = doc.CreateElement("Item" + count); // Cada objeto será un nodo <Item1>, <Item2>, etc.

                    foreach (var kvp in dictionary)
                    {
                        var element = doc.CreateElement(kvp.Key);
                        element.InnerText = kvp.Value?.ToString() ?? "";
                        itemElement.AppendChild(element);
                    }

                    root.AppendChild(itemElement); // Agregar el nodo <Item> dentro de <Data>
                    count++;
                }

                return doc.OuterXml; // Convertir el XML a string
            }

        }
        public class JsonResponse2
        {
            public string Status { get; set; }
            public string Message { get; set; }
            public string TotalRegistros { get; set; }
            public string TotalPaginas { get; set; }

            [XmlIgnore] // No serializar directamente en XML
            public List<Dictionary<string, object>> Data { get; set; }

            [XmlElement("Data")]
            public string DataXml
            {
                get
                {
                    return ListToXml(Data);
                }
                set { }
            }

            private string ListToXml(List<Dictionary<string, object>> list)
            {
                if (list == null || list.Count == 0)
                    return "<List />"; // Si la lista está vacía, devolver un nodo raíz vacío

                var doc = new XmlDocument();
                var root = doc.CreateElement("List"); // Nodo raíz único
                doc.AppendChild(root);

                int count = 1;
                foreach (var dictionary in list)
                {
                    var itemElement = doc.CreateElement("Item" + count); // Cada objeto será un nodo <Item1>, <Item2>, etc.

                    foreach (var kvp in dictionary)
                    {
                        var element = doc.CreateElement(kvp.Key);
                        element.InnerText = kvp.Value?.ToString() ?? "";
                        itemElement.AppendChild(element);
                    }

                    root.AppendChild(itemElement); // Agregar el nodo <Item> dentro de <Data>
                    count++;
                }

                return doc.OuterXml; // Convertir el XML a string
            }

        }
        public class DatosMaestrosArticulos
        {
            //Month Sales Value 12
            public string Month_Sales_Value_12 { get; set; }
            //Month Sales Volume 12
            public string Month_Sales_Volume_12 { get; set; }
            //Total Stock Value
            public string Total_Stock_Value { get; set; }
            //Total Stock Valume
            public string Total_Stock_Volume { get; set; }
            //PaisRegion
            public string PaisRegion { get; set; }
            //StatusOperational
            public string StatusOperational { get; set; }
            //SKU-Number
            public string SKUNumber { get; set; }
            //Descripcion-de-articulo
            public string Descripcion_de_Articulo { get; set; }

            //Comentarios
            public string Comentarios { get; set; }
            //01-MAS-EAS-HW-Connected-Solutions
            public string MAS_EAS_HW_Connected_Solutions { get; set; }

            //01-GA-Product-type
            public string GA_Product_Type_01 { get; set; }

            //01-GA-Product-Group


            public string GA_Product_Group_01 { get; set; }

            //01-GA-Product-Category


            public string GA_Product_Category_01 { get; set; }

            //02-MAS-Hardware-RFID


            public string MAS_Hardware_RFID { get; set; }

            //02-GA-Product-Type


            public string GA_Product_Type_02 { get; set; }

            //02-GA-Product-Group


            public string GA_Product_Group_02 { get; set; }

            //02-GA-Product-Category


            public string GA_Product_Category_02 { get; set; }

            //03-MAS-Labels-EAS


            public string MAS_Labels_EAS { get; set; }

            //03-GA-Product-type


            public string GA_Product_Type_03 { get; set; }

            //03-GA-Product-Group


            public string GA_Product_Group_03 { get; set; }

            //03-GA-Product-Category


            public string GA_Product_Category_03 { get; set; }

            //04-MAS-Alpha


            public string MAS_Alpha { get; set; }

            //04-GA-Product-Type


            public string GA_Product_Type_04 { get; set; }

            //04-GA-Product-Group


            public string GA_Product_Group_04 { get; set; }

            //04-GA-Product-Category


            public string GA_Product_Category_04 { get; set; }

            //05-MAS-Labels-RFID


            public string MAS_Labels_RFID { get; set; }

            //05-GA-Product-Type


            public string GA_Product_Type_05 { get; set; }

            //05-GA-Product-Group


            public string GA_Product_Group_05 { get; set; }

            //05-GA-Product-Category


            public string GA_Product_Category_05 { get; set; }

            //06-MAS-Hard-Tags


            public string MAS_Hard_Tags { get; set; }

            //06-GA-Product-Type


            public string GA_Product_Type_06 { get; set; }

            //06-GA-Product-Group


            public string GA_Product_Group_06 { get; set; }

            //06-GA-Product-Category


            public string GA_Product_Category_06 { get; set; }

            //07-MAS-Hard-Tags-at-Source


            public string MAS_Hard_Tags_at_Source { get; set; }

            //07-GA-Product-Type


            public string GA_Product_Type_07 { get; set; }

            //07-GA-Product-Group


            public string GA_Product_Group_07 { get; set; }

            //07-GA-Product-Category


            public string GA_Product_Category_07 { get; set; }

            //08-MAS-Field-Service


            public string MAS_Field_Service { get; set; }

            //08-GA-Product-Type


            public string GA_Product_Type_08 { get; set; }

            //08-GA-Product-Group


            public string GA_Product_Group_08 { get; set; }

            //08-GA-Product-Category


            public string GA_Product_Category_08 { get; set; }

            //09-MAS-Software


            public string MAS_Software { get; set; }

            //09-GA-Product-Type


            public string GA_Product_Type_09 { get; set; }

            //09-GA-Product-Group


            public string GA_Product_Group_09 { get; set; }

            //09-GA-Product-Category


            public string GA_Product_Category_09 { get; set; }

        }
        public class DatosMaestrosArticulosMinified
        {
            //SKU-Number
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string SKUNumber { get; set; }
            //Month Sales Value 12
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Month_Sales_Value_12 { get; set; }
            //Month Sales Volume 12
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Month_Sales_Volume_12 { get; set; }
            //Total Stock Value
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Total_Stock_Value { get; set; }
            //Total Stock Valume
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Total_Stock_Volume { get; set; }
            //PaisRegion
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string PaisRegion { get; set; }
            //StatusOperational
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string StatusOperational { get; set; }
            //Descripcion-de-articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Descripcion_de_Articulo { get; set; }
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            //Comentarios
            public string Comentarios { get; set; }

            // Campos dinámicos
            public Dictionary<string, string> Familia { get; set; }
        }
        //Impresiones de etiquetas <<--
        public class ImpresionesTubos
        {
            public string Producto { get; set; }
            public string Kilos { get; set; }
            public string Usuario { get; set; }
        }
        public class ImpresionesPacas
        {
            public string Fecha_ingreso { get; set; }
            public string Producto { get; set; }
            public string Descripcion { get; set; }
            public string Kilos { get; set; }
            public string Usuario { get; set; }
        }
        public class ConsecutivoInfo
        {
            public int Consecutivo { get; set; }
            public DateTime UltimaFechaReinicio { get; set; }
        }
        public class Consecutivos
        {
            public ConsecutivoInfo Tubos { get; set; }
            public ConsecutivoInfo Pacas { get; set; }
        }
        public class ConfiguracionImpresoras
        {
            public string Selected { get; set; }
            public string Usuario { get; set; }
        }
        public class EscaneosHHOC
        {
            public string Folio { get; set; }
            public string ID { get; set; }
        }
        public class DocumentosHHOC
        {
            public string Folio { get; set; }
        }
        #endregion
    }
}