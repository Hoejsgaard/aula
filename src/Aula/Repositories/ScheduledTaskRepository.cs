using Aula.Services;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Repositories;

public class ScheduledTaskRepository : IScheduledTaskRepository
{
	private readonly Client _supabase;
	private readonly ILogger _logger;

	public ScheduledTaskRepository(Client supabase, ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(supabase);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		_supabase = supabase;
		_logger = loggerFactory.CreateLogger<ScheduledTaskRepository>();
	}

	public async Task<List<ScheduledTask>> GetScheduledTasksAsync()
	{
		var tasksResponse = await _supabase
			.From<ScheduledTask>()
			.Where(t => t.Enabled == true)
			.Get();

		return tasksResponse.Models;
	}

	public async Task<ScheduledTask?> GetScheduledTaskAsync(string name)
	{
		var result = await _supabase
			.From<ScheduledTask>()
			.Select("*")
			.Where(st => st.Name == name)
			.Get();

		return result.Models.FirstOrDefault();
	}

	public async Task UpdateScheduledTaskAsync(ScheduledTask task)
	{
		task.UpdatedAt = DateTime.UtcNow;

		await _supabase
			.From<ScheduledTask>()
			.Update(task);

		_logger.LogInformation("Updated scheduled task: {TaskName}", task.Name);
	}
}
