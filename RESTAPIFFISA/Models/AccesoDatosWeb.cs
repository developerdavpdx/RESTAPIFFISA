namespace RESTAPIFFISA
{
    public class AccesoDatosWeb
    {
        #region GeneralCommands(Procedure declaration)

        #region WebApp
        //Comando general para obtener las OV pendientes de general la orden de fabricación
        public string GCGetOrdenesFabricacionPendientes { get { return "EXEC SpPdxFF_GetOrdenesFabricacionPendientes @PlanProduccion"; } }
        public string GCUpdatePlanProduccionDetails { get { return "EXEC SpPdxFF_UpdatePlanProduccionDetails @id,@DocNumOF,@DocEntryOF,@StatusSap,@StatusProduccion, @Orden"; } }
        #endregion

        #endregion
    }
}