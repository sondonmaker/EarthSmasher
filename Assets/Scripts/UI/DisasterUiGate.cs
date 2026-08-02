/// <summary>
/// 재해 Confirm/Report 모달 열림 여부.
/// 카메라는 막지 않고, 월드 클릭(운석 발사 등)만 막을 때 사용.
/// </summary>
public static class DisasterUiGate
{
    public static bool ModalOpen =>
        EarthquakeConfirmUI.IsOpen
        || EarthquakeReportUI.IsOpen
        || NuclearWarReportUI.IsOpen
        || MoonImpactReportUI.IsOpen;
}
