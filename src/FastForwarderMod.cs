using System;
using Game.Core.Modding;
using Shapez2Mods.FastForwarder.UI;
using UnityEngine;

namespace Shapez2Mods.FastForwarder
{
	/// <summary>
	/// Entry point for the native shapez 2 (1.2+) mod loader.
	/// The game creates exactly one <see cref="IMod"/> implementation per mod
	/// and resolves its constructor through the game's dependency container.
	/// </summary>
	public sealed class FastForwarderMod : IMod
	{
		internal static Core.Logging.ILogger Logger;

		private GameObject _uiRoot;
		private bool _uiRequested;

		public FastForwarderMod(Core.Logging.ILogger logger)
		{
			Logger = logger;

			// Mod constructors may be invoked before Unity is ready to create
			// scene objects, so the UI is instantiated on the next render pass,
			// which is guaranteed to run on the main thread.
			Application.onBeforeRender += CreateUi;

			LogInfo("Fast Forwarder loaded.");
		}

		public void Dispose()
		{
			Application.onBeforeRender -= CreateUi;
			_uiRequested = false;

			try
			{
				if (_uiRoot != null)
				{
					UnityEngine.Object.Destroy(_uiRoot);
					_uiRoot = null;
				}
			}
			catch
			{
				// Unity objects may already be gone during shutdown.
			}
		}

		private void CreateUi()
		{
			Application.onBeforeRender -= CreateUi;

			if (_uiRequested)
			{
				return;
			}

			_uiRequested = true;

			try
			{
				_uiRoot = new GameObject("FastForwarder_UI_Root");
				UnityEngine.Object.DontDestroyOnLoad(_uiRoot);
				_uiRoot.AddComponent<FastForwarderUI>();
				LogInfo("UI created.");
			}
			catch (Exception ex)
			{
				LogError("Failed to create UI: " + ex);
			}
		}

		internal static void LogInfo(string message)
		{
			Logger?.Info?.Log("[FastForwarder] " + message);
		}

		internal static void LogWarning(string message)
		{
			Logger?.Warning?.Log("[FastForwarder] " + message);
		}

		internal static void LogError(string message)
		{
			if (Logger?.Error != null)
			{
				Logger.Error.Log("[FastForwarder] " + message);
			}
			else
			{
				Debug.LogError("[FastForwarder] " + message);
			}
		}
	}
}
