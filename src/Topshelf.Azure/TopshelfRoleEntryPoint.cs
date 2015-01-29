namespace Topshelf
{
    using HostConfigurators;


    public interface TophelfServiceConfigurator
    {
        void Configure(HostConfigurator hostConfigurator);
    }
}