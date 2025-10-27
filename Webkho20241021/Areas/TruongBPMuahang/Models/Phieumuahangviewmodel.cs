using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPMuahang.Data
{
    public class Phieumuahangviewmodel
    {
        public string MaMuahang { get; set; }
        public List<phieumuahang> Phieumuahang { get; set; }
        public List<vtphieumuahang> VTphieumuahang { get; set; }

    }
}
