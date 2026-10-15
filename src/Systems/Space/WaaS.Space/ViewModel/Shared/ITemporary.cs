using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public interface ITemporary
{
    TemporaryInfo? Temporary { get; set; }
}