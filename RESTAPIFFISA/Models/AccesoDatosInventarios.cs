using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace RESTAPIFFISA
{
    public class AccesoDatosInventarios
    {
        #region GeneralCommands(Procedure declaration) Entradas
        //Comando general para obtener las OV pendientes de general la orden de fabricación
        public string GCUpdatePlantillaEtiquetaHHEMD { get { return "EXEC SpPdxFF_UpdatePlantillaEtiquetaHHEMD @Plantilla,@Usuario"; } }
        public string GCGetPlantillaEtiquetaHHEMD { get { return "EXEC SpPdxFF_GetPlantillaEtiquetaHHEMD @Usuario"; } }
        public string GCInsertaEncabezadoEntradasHHEMD { get { return "EXEC SpPdxFF_InsertaEncabezadoEntradasHHEMD @Usuario"; } }
        public string GCInsertaLineasEntradasHHEMD { get { return "EXEC SpPdxFF_InsertaLineasEntradasHHEMD @Folio,@Articulo,@Etiqueta,@Almacen,@Lote,@Cantidad,@CantidadMetros,@Comentarios,@CuentaContable,@CodigoCuentaContable"; } }
        public string GCEliminaEscaneosEntradasHHEMD { get { return "EXEC SpPdxFF_EliminaEscaneosEntradasHHEMD @ID,@Folio"; } }
        public string GCEliminaDocumentosEntradasHHEMD { get { return "EXEC SpPdxFF_EliminaDocumentosEntradasHHEMD @Folio"; } }
        public string GCEliminaDocumentosVaciosEntradasHHEMD { get { return "EXEC SpPdxFF_EliminaDocumentosVaciosEntradasHHEMD"; } }
        public string GCEliminaDocumentosVaciosSalidasHHSMD { get { return "EXEC SpPdxFF_EliminaDocumentosVaciosSalidasHHSMD"; } }
        public string GCEliminaDocumentosVaciosRecuentosHHRI { get { return "EXEC SpPdxFF_EliminaDocumentosVaciosRecuentosHHRI"; } }
        public string GCEliminaDocumentosVaciosTransferenciasHHTS { get { return "EXEC SpPdxFF_EliminaDocumentosVaciosTransferenciasHHTS"; } }
        public string GCGetArticulosHHEMD { get { return "EXEC SpPdxFF_GetArticulosHHEMD @Busqueda"; } } //Reutilizado
        public string GCGetDocumentosEntradasHHEMD { get { return "EXEC SpPdxFF_GetDocumentosEntradasHHEMD @FI,@FF,@Usuario"; } }
        public string GCGetLinesDocumentosEntradasHHEMD { get { return "EXEC SpPdxFF_GetLinesDocumentosEntradasHHEMD @Folio,@Usuario"; } }
        public string GCValidaAlmacenHHEMD { get { return "EXEC SpPdxFF_ValidaAlmacenHHEMD @Almacen"; } }
        public string GCGetCuentasContablesHHEMD { get { return "EXEC SpPdxFF_GetCuentasContablesHHEMD @Busqueda"; } }
        public string GCActualizaDocumentosEntradasHHEMD { get { return "EXEC SpPdxFF_ActualizaDocumentosEntradasHHEMD @Folio,@EntradaMercancia"; } }
        public string GCActualizaLoteEntradasHHEMD { get { return "EXEC SpPdxFF_ActualizaLoteEntradasHHEMD @BatchNumber,@ItemCode,@Cantidadenmetros,@Comentarios"; } }
        public string GCActualizaLinesDocumentosEntradasHHEMD { get { return "EXEC SpPdxFF_ActualizaLinesDocumentosEntradasHHEMD @ID,@Lote,@Cantidadenmetros"; } }
        public string GCObtenerDatosEtiquetaHHEMD { get { return "EXEC SpPdxFF_ObtenerDatosEtiquetaHHEMD @DistNumber"; } }
        public string GCGetNomenclaturaEtiqueta { get { return "EXEC SpPdxFF_GetNomenclaturaEtiqueta @Plantilla,@ItemCode"; } }
        public string GCRequiereCantidadMetrosHHEMD { get { return "EXEC SpPdxFF_RequiereCantidadMetrosHHEMD @ItemCode"; } }
        #endregion

        #region GeneralCommands(Procedure declaration) Salidas
        public string GCInsertaEncabezadoSalidasHHEMD { get { return "EXEC SpPdxFF_InsertaEncabezadoSalidasHHEMD @Usuario"; } }
        public string GCValidarLoteInventariosHH { get { return "EXEC SpPdxFF_ValidarLoteInventariosHH @ItemCode,@Lote,@Folio"; } }
        public string GCValidarLoteInventariosHHEMD { get { return "EXEC SpPdxFF_ValidarLoteInventariosHHEMD @ItemCode,@Lote,@Folio"; } }
        public string GCInsertaLineasSalidasHHEMD { get { return "EXEC SpPdxFF_InsertaLineasSalidasHHEMD @Folio,@Articulo,@Almacen,@Lote,@Cantidad,@CantidadMetros,@Comentarios,@CuentaContable,@CodigoCuentaContable"; } }
        public string GCEliminaEscaneosSalidasHHEMD { get { return "EXEC SpPdxFF_EliminaEscaneosSalidasHHEMD @ID,@Folio"; } }
        public string GCEliminaDocumentosSalidasHHEMD { get { return "EXEC SpPdxFF_EliminaDocumentosSalidasHHEMD @Folio"; } }
        public string GCGetDocumentosSalidasHHEMD { get { return "EXEC SpPdxFF_GetDocumentosSalidasHHEMD @FI,@FF,@Usuario"; } }
        public string GCGetLinesDocumentosSalidasHHEMD { get { return "EXEC SpPdxFF_GetLinesDocumentosSalidasHHEMD @Folio,@Usuario"; } }
        public string GCActualizaDocumentosSalidasHHEMD { get { return "EXEC SpPdxFF_ActualizaDocumentosSalidasHHEMD @Folio,@SalidaMercancia"; } }

        #endregion

        #region GeneralCommands(Procedure declaration) Transferencias
        //Comando general para obtener las OV pendientes de general la orden de fabricación
        public string GCInsertaEncabezadoTransferenciasHHTS { get { return "EXEC SpPdxFF_InsertaEncabezadoTransferenciasHHTS @Usuario"; } }
        public string GCGeDocumentosTransferenciasHHTS { get { return "EXEC SpPdxFF_GetDocumentosTransferenciasHHTS  @FI,@FF,@Usuario"; } }
        public string GCValidarLoteTraspasosHHTM { get { return "EXEC SpPdxFF_ValidarLoteTraspasosHHTM @Lote"; } }
        public string GCInsertaLineasTraspasosHHEMD { get { return "EXEC SpPdxFF_InsertaLineasTraspasosHHEMD @FolioTS,@LoteOrigen,@Cantidad,@AlmacenOrigen,@AlmacenDestino,@Articulo,@Comentarios"; } }
        public string GCEliminaEscaneosTransferenciasHHTS { get { return "EXEC SpPdxFF_EliminaEscaneosTransferenciasHHTS @ID,@Folio"; } }
        public string GCEliminaDocumentosTransferenciasHHTS { get { return "EXEC SpPdxFF_EliminaDocumentosTransferenciasHHTS @Folio"; } }
        public string GCGetLinesDocumentosTransferenciasHHTS { get { return "EXEC SpPdxFF_GetLinesDocumentosTransferenciasHHTS @Folio,@Usuario"; } }
        public string GCActualizaDocumentosTraspasosHHEMD { get { return "EXEC SpPdxFF_ActualizaDocumentosTraspasosHHEMD @Folio,@TransferenciaStock"; } }
        #endregion

        #region GeneralCommands(Procedure declaration) Recuentos
        //Comando general para obtener las OV pendientes de general la orden de fabricación
        public string GCInsertaEncabezadoRecuentosHHRI { get { return "EXEC SpPdxFF_InsertaEncabezadoRecuentosHHRI @Usuario,@DocEntry,@SapDocument"; } }
        public string GCGeDocumentosRecuentosHHRI { get { return "EXEC SpPdxFF_GetDocumentosRecuentosHHRI  @FI,@FF,@Usuario"; } }
        public string GCGetRecuentosInventarioHHRI { get { return "SpPdxFF_GetRecuentosInventarioHHRI @Recuento"; } }
        public string GCValidarLoteRecuentoHHRI { get { return "EXEC SpPdxFF_ValidarLoteRecuentoHHRI @Lote,@DocNum"; } }
        public string GCInsertaLineasRecuentosHHRI { get { return "EXEC SpPdxFF_InsertaLineasRecuentosHHRI @FolioRI,@Lote,@Cantidad,@Articulo,@Almacen,@ExisteEnRecuento"; } }
        public string GCGetTotalRecuentoHHRI { get { return "EXEC SpPdxFF_GetTotalRecuentoHHRI @FolioRI,@Usuario"; } }
        public string GCGetTotalTraspasosHHRI { get { return "EXEC SpPdxFF_GetTotalTraspasoHHRI @FolioTS,@Usuario"; } }
        public string GCGetLinesDocumentosRecuentosHHRI { get { return "EXEC SpPdxFF_GetLinesDocumentosRecuentosHHRI @Folio,@Usuario"; } }
        public string GCGetLinesDocumentosRecuentosGroupedHHRI { get { return "EXEC SpPdxFF_GetLinesDocumentosRecuentosGroupedHHRI @Folio,@Usuario"; } }
        public string GCEliminaEscaneosRecuentosHHRI { get { return "EXEC SpPdxFF_EliminaEscaneosRecuentosHHRI @ID,@Folio"; } }
        public string GCEliminaDocumentosRecuentosHHRI { get { return "EXEC SpPdxFF_EliminaDocumentosRecuentosHHRI  @Folio"; } }
        public string GCActualizaDocumentosRecuentosHHRI { get { return "EXEC SpPdxFF_ActualizaDocumentosRecuentosHHRI @Folio,@RecuentoInventario"; } }
        #endregion

        #region AditionalClassModel
        public class PlantillasEtiquetaEMD
        {
            public string Plantilla { get; set; }
            public string Usuario { get; set; }
        }

        public class EncabezadoEntradasHHEMD
        {
            public string Usuario { get; set; }
        }
        public class EncabezadoSalidasHHEMD
        {
            public string Usuario { get; set; }
        }
        public class EncabezadoTransferenciasHHTS
        {
            public string Usuario { get; set; }
        }
        public class EncabezadoRecuentosHHRI
        {
            public string Usuario { get; set; }
            public string DocEntry { get; set; }
            public string SapDocument { get; set; }
        }
        public class EscaneosEntradasHHEMD
        {
            public string Folio { get; set; }
            public string ID { get; set; }
        }
        public class EscaneosSalidasHHEMD
        {
            public string Folio { get; set; }
            public string ID { get; set; }
        }
        public class EscaneosTransferenciasHHTS
        {
            public string Folio { get; set; }
            public string ID { get; set; }
        }
        public class EscaneosRecuentosHHRI
        {
            public string Folio { get; set; }
            public string ID { get; set; }
            public string Usuario { get; set; }
        }
        public class DocumentosEntradasHHEMD
        {
            public string Folio { get; set; }
        }
        public class DocumentosSalidasHHEMD
        {
            public string Folio { get; set; }
        }
        public class DocumentosTransferenciasHHTS
        {
            public string Folio { get; set; }
        }
        public class DocumentosRecuentosHHRI
        {
            public string Folio { get; set; }
            public string Usuario { get; set; }
        }
        public class EntradasMercanciaHHEMD
        {
            public string FolioEM { get; set; }
            public DateTime FechaEntrada { get; set; }
            public string Usuario { get; set; }
        }
        public class SalidasMercanciaHHEMD
        {
            public string FolioSM { get; set; }
            public DateTime FechaSalida { get; set; }
            public string Usuario { get; set; }
        }
        public class TraspasosMercanciaHHEMD
        {
            public string FolioTM { get; set; }
            public string Usuario { get; set; }
        }
        public class RecuentoInventarioHHRI
        {
            public string DocEntry { get; set; }
            public string FolioRI { get; set; }
            public string Usuario { get; set; }
        }
        public class LineasEntradasHHEMD
        {
            // Folio
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Folio { get; set; } = "";

            // Articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Articulo { get; set; } = "";


            // Etiqueta
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Etiqueta { get; set; } = "";

            // Almacen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Almacen { get; set; } = "";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Lote
            {
                get => _lote;
                set => _lote = string.IsNullOrWhiteSpace(value) ? null : value;
            }
            private string _lote;

            // Cantidad
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cantidad { get; set; } = "";

            // CantidadMetros
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CantidadMetros { get; set; } = "";

            // Comentarios
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Comentarios { get; set; } = "";

            // CuentaContable
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CuentaContable { get; set; } = "";

            // CodigoCuentaContable
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CodigoCuentaContable { get; set; } = "";
        }
        public class LineasSalidasHHEMD
        {
            // Folio
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Folio { get; set; } = "";

            // Articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Articulo { get; set; } = "";

            // Almacen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Almacen { get; set; } = "";

            // Lote
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Lote { get; set; } = "";

            // Cantidad
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cantidad { get; set; } = "";

            // CantidadMetros
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CantidadMetros { get; set; } = "";

            // Comentarios
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Comentarios { get; set; } = "";

            // CuentaContable
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CuentaContable { get; set; } = "";

            // CodigoCuentaContable
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string CodigoCuentaContable { get; set; } = "";
        }
        public class LineasTransferenciasHHTS
        {
            // Folio
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string FolioTS { get; set; } = "";

            // Lote Origen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string LoteOrigen { get; set; } = "";

            // Cantidad
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cantidad { get; set; } = "";

            // Almacen Origen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string AlmacenOrigen { get; set; } = "";

            // AlmacenDestino
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string AlmacenDestino { get; set; } = "";

            // Articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Articulo { get; set; } = "";

            // Comentarios
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Comentarios { get; set; } = "";

        }
        public class LineasRecuentosHHRI
        {
            // Folio
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string FolioRI { get; set; } = "";

            // Lote
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Lote { get; set; } = "";

            // Cantidad
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Cantidad { get; set; } = "";

            // Articulo
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Articulo { get; set; } = "";

            // Almacen
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Almacen { get; set; } = "";
            // ExisteEnRecuento
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string ExisteEnRecuento { get; set; } = "";

            // Usuario
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue("")]
            public string Usuario { get; set; } = "";

        }
        public class ConsecutivosEntradas
        {
            public ConsecutivoInfoED EntradasDirectas { get; set; }
        }
        public class ConsecutivoInfoED
        {
            public int Consecutivo { get; set; }
            public DateTime UltimaFechaReinicio { get; set; }
        }
        public class ConsecutivosMateriaPrima
        {
            public ConsecutivoInfoMP Tubos { get; set; }
            public ConsecutivoInfoMP Pacas { get; set; }
        }
        public class ConsecutivoInfoMP
        {
            public int Consecutivo { get; set; }
            public DateTime UltimaFechaReinicio { get; set; }
        }
        #endregion
    }
}