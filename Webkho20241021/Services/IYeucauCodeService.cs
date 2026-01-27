using Microsoft.AspNetCore.Http;

public interface IYeucauCodeService
{
    string GenerateMaYeucauCommon(
        string? ycMaDuan,
        List<string>? maSanpham,
        IFormFileCollection? files,
        DateTime now);
}
