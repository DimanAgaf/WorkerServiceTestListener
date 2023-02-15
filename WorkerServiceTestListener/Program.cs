using WorkerServiceTestListener;

IHost host = Host.CreateDefaultBuilder(args)
	.UseWindowsService()
	.UseSystemd()
	.ConfigureServices(services =>
	{
		services.AddHostedService<Worker>();
	})
	.Build();

host.Run();
