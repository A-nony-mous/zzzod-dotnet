using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试业务核心。
/// </summary>
public sealed class OperationDebugService : IDisposable
{
	private readonly OperationDebugConfig _config;

	private readonly OperationDebugTemplateLoader _templateLoader;

	private readonly IOperationDebugAtomicOpFactory _atomicOpFactory;

	private readonly IOperationDebugControllerModeSwitcher _controllerModeSwitcher;

	private readonly List<AtomicOp> _operations = new List<AtomicOp>();

	private readonly object _operationsLock = new object();

	private readonly HashSet<AtomicOp> _stoppedOperations = new HashSet<AtomicOp>(ReferenceEqualityComparer.Instance);

	private AtomicOp? _runningOperation;

	private bool _disposeRequested;

	private bool _disposed;

	private int _operationIndex;

	/// <summary>
	/// 当前已加载的操作。
	/// </summary>
	public IReadOnlyList<AtomicOp> Operations
	{
		get
		{
			lock (_operationsLock)
			{
				return _operations.ToArray();
			}
		}
	}

	/// <summary>
	/// 当前操作下标。
	/// </summary>
	public int OperationIndex
	{
		get
		{
			lock (_operationsLock)
			{
				return _operationIndex;
			}
		}
	}

	/// <summary>
	/// 初始化服务。
	/// </summary>
	public OperationDebugService(OperationDebugConfig config, OperationDebugTemplateLoader templateLoader, IOperationDebugAtomicOpFactory atomicOpFactory, IOperationDebugControllerModeSwitcher controllerModeSwitcher)
	{
		_config = config;
		_templateLoader = templateLoader;
		_atomicOpFactory = atomicOpFactory;
		_controllerModeSwitcher = controllerModeSwitcher;
	}

	/// <summary>
	/// 检查控制方式。
	/// </summary>
	public OperationDebugControllerModeResult CheckGamepad()
	{
		return _controllerModeSwitcher.CheckAndApply();
	}

	/// <summary>
	/// 加载动作指令。
	/// </summary>
	public OperationDebugStepResult LoadOperations()
	{
		List<AtomicOp> list = new List<AtomicOp>();
		try
		{
			foreach (OperationDef item in _templateLoader.LoadOperations(_config.OperationTemplate))
			{
				list.Add(_atomicOpFactory.Create(item));
			}
		}
		catch
		{
			ReleaseUntrackedOperations(list);
			throw;
		}
		List<AtomicOp> operations;
		try
		{
			lock (_operationsLock)
			{
				ThrowIfDisposed();
				if (_runningOperation != null)
				{
					throw new InvalidOperationException("正在执行指令，无法加载新的动作模板。");
				}
				operations = _operations.ToList();
				_operations.Clear();
				_operations.AddRange(list);
				foreach (AtomicOp item2 in list)
				{
					_stoppedOperations.Remove(item2);
				}
				_operationIndex = 0;
			}
		}
		catch
		{
			ReleaseUntrackedOperations(list);
			throw;
		}
		ReleaseTrackedOperations(operations);
		return (list.Count == 0) ? new OperationDebugStepResult(IsSuccess: false, Completed: true, "操作模板中没有找到可执行的操作") : new OperationDebugStepResult(IsSuccess: true, Completed: false, null);
	}

	/// <summary>
	/// 执行下一条指令。
	/// </summary>
	public OperationDebugStepResult RunNextOperation()
	{
		AtomicOp atomicOp;
		lock (_operationsLock)
		{
			ThrowIfDisposed();
			if (_operations.Count == 0)
			{
				return new OperationDebugStepResult(IsSuccess: false, Completed: true, "操作模板中没有找到可执行的操作");
			}
			atomicOp = (_runningOperation = _operations[_operationIndex]);
		}
		try
		{
			atomicOp.Execute();
		}
		finally
		{
			List<AtomicOp> list = null;
			lock (_operationsLock)
			{
				_runningOperation = null;
				if (_disposeRequested)
				{
					list = _operations.ToList();
					_operations.Clear();
					_operationIndex = 0;
					_disposed = true;
				}
			}
			if (list != null)
			{
				ReleaseTrackedOperations(list);
			}
		}
		lock (_operationsLock)
		{
			if (_disposed)
			{
				return new OperationDebugStepResult(IsSuccess: false, Completed: true, "指令调试已停止");
			}
			_operationIndex++;
			if (_operationIndex < _operations.Count)
			{
				return new OperationDebugStepResult(IsSuccess: true, Completed: false, null);
			}
			if (_config.RepeatEnabled)
			{
				_operationIndex = 0;
				return new OperationDebugStepResult(IsSuccess: true, Completed: false, "repeat");
			}
			return new OperationDebugStepResult(IsSuccess: true, Completed: true, null);
		}
	}

	/// <summary>
	/// 停止当前原子操作，让同步等待能够退出。
	/// </summary>
	public void Stop()
	{
		AtomicOp[] unstoppedOperationsLocked;
		lock (_operationsLock)
		{
			unstoppedOperationsLocked = GetUnstoppedOperationsLocked(_operations);
		}
		StopOperations(unstoppedOperationsLocked);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		List<AtomicOp> list = null;
		lock (_operationsLock)
		{
			if (_disposed)
			{
				return;
			}
			_disposeRequested = true;
			if (_runningOperation == null)
			{
				list = _operations.ToList();
				_operations.Clear();
				_operationIndex = 0;
				_disposed = true;
			}
		}
		Stop();
		if (list != null)
		{
			ReleaseTrackedOperations(list);
		}
	}

	private void ReleaseTrackedOperations(IEnumerable<AtomicOp> operations)
	{
		AtomicOp[] unstoppedOperationsLocked;
		lock (_operationsLock)
		{
			unstoppedOperationsLocked = GetUnstoppedOperationsLocked(operations);
		}
		StopOperations(unstoppedOperationsLocked);
		foreach (AtomicOp operation in operations)
		{
			operation.Dispose();
		}
	}

	private static void ReleaseUntrackedOperations(IEnumerable<AtomicOp> operations)
	{
		foreach (AtomicOp operation in operations)
		{
			operation.Stop();
			operation.Dispose();
		}
	}

	private AtomicOp[] GetUnstoppedOperationsLocked(IEnumerable<AtomicOp> operations)
	{
		return operations.Where(_stoppedOperations.Add).ToArray();
	}

	private static void StopOperations(IEnumerable<AtomicOp> operations)
	{
		foreach (AtomicOp operation in operations)
		{
			operation.Stop();
		}
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposeRequested, this);
	}
}
