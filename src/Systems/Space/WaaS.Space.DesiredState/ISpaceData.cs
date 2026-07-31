namespace WaaS.Space.DesiredState;

public interface ISpaceData<out T> where T : IWebspace
{
    T Space { get; }
}