using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace RESTAPIFFISA
{
    public class AccesoDatosVentas
    {
        #region GeneralCommands(Procedure declaration)
        //Comando general para obtener las OV pendientes de general la orden de fabricación
        public string GCGetOrdenesVentaHH { get { return "EXEC SpPdxFF_GetOrdenesVentaHH @OV"; } }
        public string GCGetArticulosOVHH { get { return "EXEC SpPdxFF_GetArticulosOVHH @OV"; } }
        public string GCGetDocumentosVentasHHEMOV { get { return "EXEC SpPdxFF_GetDocumentosVentasHHEMOV @FI,@FF,@Usuario"; } }
        public string GCGetLinesDocumentosVentasHHEMOV { get { return "EXEC SpPdxFF_GetLinesDocumentosVentasHHEMOV @Folio,@Usuario"; } }
        public string GCEliminaEscaneosVentasHHEMOV { get { return "EXEC SpPdxFF_EliminaEscaneosVentasHHEMOV @ID,@Folio"; } }
        public string GCValidarLoteVentasHHEMOV { get { return "EXEC SpPdxFF_ValidarLoteVentasHHEMOV @ItemCode,@Lote,@WhsOV,@UMS"; } }
        public string GCValidarOrdenVentaHHEMOV { get { return "EXEC SpPdxFF_ValidarOrdenVentaHHEMOV @DocEntry"; } }
        public string GCInsertaEncabezadoVentasHHEMOV { get { return "EXEC SpPdxFF_InsertaEncabezadoVentasHHEMOV @OrdenVenta,@SapDocument,@Cliente,@Usuario,@Comentarios,@Estatus"; } }
        public string GCInsertaLineasVentasHHEMOV { get { return "EXEC SpPdxFF_InsertaLineasVentasHHEMOV @Folio,@Articulo,@Linea,@Almacen,@UMV,@UMI,@UMS,@Lote,@Cantidad,@CantidadConversion,@Referencia,@CantidadReferencia"; } }
        public string GCEliminaDocumentosVentasEMOV { get { return "EXEC SpPdxFF_EliminaDocumentosVentasHHEMOV @Folio"; } }
        public string GCGetRollosHHEMOV { get { return "EXEC SpPdxFF_GetRollosHHEMOV @Folio"; } }
        public string GCActualizaDocumentosVentasHHEMOV { get { return "EXEC SpPdxFF_ActualizaDocumentosVentasHHEMOV @OrdenVenta,@Folio,@EntregaMercancia,@Borrador"; } }
        public string GCActualizaLoteVentasHHEMOV { get { return "EXEC SpPdxFF_ActualizaLoteVentasHHEMOV @BatchNumber,@ItemCode,@Fecha,@OrdenVenta,@Cliente,@CantidadReferencia"; } }
        public string GCActualizaMontoLetrasHHEMOV { get { return "EXEC SpPdxFF_ActualizaMontoLetrasHHEMOV @Entrega,@MontoES,@MontoEN,@TipoDocumento"; } }
        public string GCValidarEntregaExistenteHHEMOV { get { return "EXEC SpPdxFFValidarEntregaExistenteHHEMOV @Folio,@Usuario"; } }
        public string GCValidarDraftEntregaExistenteHHEMOV { get { return "EXEC SpPdxFFValidarDraftEntregaExistenteHHEMOV @Folio,@Usuario"; } }
        public string GCValidarOrdenVentaCompletaPorRollosHH { get { return "EXEC SpPdxFFValidarOrdenVentaCompletaPorRollosHH @DocEntry"; } }
        public string GCValidarFolioEntregaPendienteHHEMOV { get { return "EXEC SpPdxFFValidarFolioEntregaPendienteHHEMOV @SapDocument,@Usuario"; } }
        #endregion

        #region AditionalClassModel
        public class EncabezadoVentasHHEMOV
        {
            //OrdenVenta
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string OrdenVenta { get; set; }
            //SapDocument
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string SapDocument { get; set; }
            //Cliente
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cliente { get; set; }
            //Usuario
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Usuario { get; set; }
            //Comentarios
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Comentarios { get; set; }
            //Estatus
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Estatus { get; set; }
        }
        public class LineasVentasHHEMOV
        {
            //Folio
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Folio { get; set; }
            //Articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Articulo { get; set; }
            //Linea
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Linea { get; set; }
            //Almacen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Almacen { get; set; }
            //UMV
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string UMV { get; set; }
            //UMI
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string UMI { get; set; }
            //UMS
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string UMS { get; set; }
            //Lote
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Lote { get; set; }
            //Cantidad
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cantidad { get; set; }
            //Referencia
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CantidadConversion { get; set; }
            //Referencia
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Referencia { get; set; }
            //CantidadReferencia
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CantidadReferencia { get; set; }
            //Usuario
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Usuario { get; set; }
        }
        public class DocumentosVentasEMOV
        {
            public string Folio { get; set; }
            public string Usuario { get; set; }
        }

        public class EscaneosVentasHHEMOV
        {
            public string Folio { get; set; }
            public string ID { get; set; }
            public string Usuario { get; set; }
        }
        public class EntregasMercanciaVentasHHEMOV
        {
            public string FolioEM { get; set; }
            public int OrdenVenta { get; set; }
            public DateTime FechaEntrega { get; set; }
            public string IsBorrador { get; set; }
            public string Usuario { get; set; }
            public string Comentarios { get; set; }
            public string Rollos { get; set; }
        }
        [Serializable]
        public class CancelacionEntregaRequest
        {
            public string DocEntry { get; set; }
            public string Usuario { get; set; } // Opcional, por si quieres auditar
            public string Motivo { get; set; }   // Opcional, por si quieres registrar el motivo
        }
        #endregion
    }
}