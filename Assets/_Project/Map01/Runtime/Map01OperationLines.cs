using L=ShadowVale.Map01.Map01StoryDialogue.Line;
namespace ShadowVale.Map01
{
    public static class Map01OperationLines
    {
        public static L Acknowledge()=>new L("Nam","Rõ.",false,"m1_v2_rescue_approach_03");
        public static L[] SupplyOrder=>new[]{
            new L("Chỉ huy","Số thuốc của Hùng còn ở điểm một. Lấy lại rồi mang về hầm, anh em đang cần.",false,"m1_ops_supply_order_01"),
            new L("Hùng","Hàng nằm trong kho. Chìa do tên gác giữ.",false,"m1_ops_supply_order_02"),
            Acknowledge()};
        public static L[] SupplyReport=>new[]{
            new L("Nam","Tôi lấy được túi tiếp tế rồi. Thuốc vẫn còn nguyên.",false,"m1_ops_supply_report_01"),
            new L("Chỉ huy","Mang về hầm. Đi cẩn thận, chúng có thể còn lính ngoài đường.",true,"m1_ops_supply_report_02"),
            Acknowledge()};
        public static L[] Handoff=>new[]{
            new L("Nam","Hàng đây. Tôi đã kiểm tra, thuốc còn dùng được.",false,"m1_ops_handoff_01"),
            new L("Hùng","Đúng số hàng của tôi rồi.",false,"m1_ops_handoff_02"),
            new L("Chỉ huy","Đài ở điểm hai vẫn đang điều lính ra các chốt. Nam, tới đó vô hiệu hóa nó, cắt đường gọi viện của chúng.",false,"m1_ops_handoff_03"),
            Acknowledge()};
        public static L[] RadioReport=>new[]{
            new L("Nam","Đài đã ngừng phát. Chúng không gọi viện qua trạm này được nữa.",false,"m1_ops_radio_report_01"),
            new L("Chỉ huy","Tên sĩ quan điều quân đang ở điểm ba. Quan sát từ xa rồi báo tôi trước khi đánh.",true,"m1_ops_radio_report_02"),
            Acknowledge()};
        public static L[] CommanderReport=>new[]{
            new L("Nam","Tôi thấy tên sĩ quan rồi. Hắn ở trong chốt, có lính bảo vệ.",false,"m1_ops_commander_report_01"),
            new L("Chỉ huy","Hắn đang siết đường ra bến. Chọn vị trí có vật che, hạ hắn rồi rút.",true,"m1_ops_commander_report_02"),
            Acknowledge()};
        public static L[] EarlyCommanderReport=>new[]{new L("Nam","Tôi đã xác nhận. Tên sĩ quan bị hạ trong lúc giao tranh.",false,"m1_ops_commander_early_01")};
        public static L[] RetreatReport=>new[]{
            new L("Nam","Tên sĩ quan đã bị hạ. Tôi đang rời chốt.",false,"m1_ops_retreat_01"),
            new L("Chỉ huy","Ra bến phía bắc. Tôi và Hùng chờ cậu trên thuyền.",true,"m1_ops_retreat_02"),
            Acknowledge()};
    }
}
