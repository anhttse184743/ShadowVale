namespace ShadowVale.Map01
{
    /// <summary>Approved spoken C3 cues. Gameplay notices remain separate from character speech.</summary>
    public static class Map01C3Dialogue
    {
        public const string CheckInCall = "Lính gác cổng (bộ đàm): Tổ một, tổ hai — báo cáo.";
        public const string CheckInReply = "Lính tuần tra (bộ đàm): Bình thường.";
        public const string NoAnswer = "Lính gác cổng (bộ đàm): Tổ hai, trả lời!";
        public const string Alarm = "Lính gác cổng (bộ đàm): Mất liên lạc! Cả trại cảnh giác!";
        public const string EnemyCall = "Chỉ huy địch: Lính đâu! Ra hết đây!";
        public const string MosinInstruction = "Trinh sát để lại một khẩu Mosin, năm viên, ở vai đồi phía Đông Nam điểm ba. Từ đó nhìn được chòi canh.";
        public const string MosinOrder = "Chỉ huy (bộ đàm): " + MosinInstruction;
        public const string MalletRemoved = "Nam: Xong. Chúng không gõ kẻng được nữa.";
        public const string PhoneCut = "Nam: Đường dây đã ngắt.";
        public static string CheckIn => CheckInCall + "\n" + CheckInReply;
        public static string MissingPatrol => NoAnswer + "\n" + Alarm;
        public static string CommanderOrder => MosinOrder + "\nNam: Rõ.";
    }
}
