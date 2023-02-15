using Azure;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorkerServiceTestListener
{
	public class Worker : IHostedService, IDisposable
	{
		private readonly ILogger<Worker> _logger;
		private readonly IConfiguration _configuration;
		private Timer? _timer = null;

		public Worker(ILogger<Worker> logger, IConfiguration configuration)
		{
			_logger = logger;
			_configuration = configuration;
		}
		public Task StartAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Timed Hosted Service running.");

			_timer = new Timer(DoWork, null, TimeSpan.Zero,
				TimeSpan.FromMinutes(Convert.ToDouble(_configuration.GetSection("TimerTimeSpanFromMinutes").Value)));

			return Task.CompletedTask;
		}
		private async void DoWork(object? state)
		{
			_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

			await AddRequestAsync();
			IEnumerable<Request> getRequests = await GetRequestAsync();

			if (getRequests.Count() != 0)
			{
				await HttpRequestAsync(getRequests);
			}
		}
		public Task StopAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Timed Hosted Service is stopping.");

			_timer?.Change(Timeout.Infinite, 0);

			return Task.CompletedTask;
		}
		public void Dispose()
		{
			_timer?.Dispose();
		}
		async Task<IEnumerable<Request>> GetRequestAsync()
		{
			using (AppDbContext db = new AppDbContext())
			{
				return await db.Requests.Where(w => w.status == "В работе").ToListAsync();
			}
		}
		async Task AddRequestAsync()
		{
			using (AppDbContext db = new AppDbContext())
			{
				try
				{
					Request r = new Request { status = "В работе", message = String.Empty };
					await db.Requests.AddAsync(r);
					await db.SaveChangesAsync();

					_logger.LogInformation("Add new item on db at: {time}", DateTimeOffset.Now);
				}
				catch (Exception ex)
				{
					_logger.LogInformation(ex.Message);
				}
			}			
		}
		async Task UpdateRangeRequestAsync(IEnumerable<Request> requests)
		{
			using (AppDbContext db = new AppDbContext())
			{
				try
				{
					db.UpdateRange(requests);
					await db.SaveChangesAsync();

					_logger.LogInformation("Update items on db at: {time}", DateTimeOffset.Now);
				}
				catch (Exception ex)
				{
					_logger.LogInformation(ex.Message);
				}
			}
		}
		static readonly HttpClient httpClient = new HttpClient();
		async Task HttpRequestAsync(IEnumerable<Request> requests)
		{
			try
			{
				HttpResponseMessage response = await httpClient.PostAsJsonAsync(_configuration.GetSection("UrlAPI").Value, requests);
				if (response.IsSuccessStatusCode)
				{
					string res = response.Content.ReadAsStringAsync().Result;
					IEnumerable<Request>? httpRequests = JsonSerializer.Deserialize<IEnumerable<Request>>(res);
					if (httpRequests is not null)
					await UpdateRangeRequestAsync(httpRequests);
				}
			}
			catch (HttpRequestException ex)
			{
				_logger.LogInformation(ex.Message);
			}
		}
	}
}