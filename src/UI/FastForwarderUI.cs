using System;
using UnityEngine;
using UnityEngine.UI;

namespace Shapez2Mods.FastForwarder.UI
{
	public sealed class FastForwarderUI : MonoBehaviour
	{
		private static readonly float[] Speeds = { 0f, 1f, 5f, 10f, 25f };

		private static readonly string[] Labels = { "II", ">", ">>", ">>>", ">|" };

		private static readonly string[] Tooltips =
		{
			"Pause (0x)",
			"Normal (1x)",
			"Fast (5x)",
			"Very fast (10x)",
			"Ultra (25x)"
		};

		private const float PanelWidth = 200f;
		private const float PanelHeight = 40f;
		private const float PanelRightMargin = 10f;
		private const float PanelTop = 120f;

		private GameObject _raycastBlocker;
		private GUIStyle _buttonStyle;
		private int _selectedIndex = 1;
		private int _hoveredIndex = -1;
		private object _hookedSession;
		private SimulationSpeedManager _simManager;
		private bool _lastGameState;

		private void Start()
		{
			try
			{
				CreateRaycastBlocker();
				SetBlockerVisible(_simManager != null);
			}
			catch (Exception ex)
			{
				FastForwarderMod.LogWarning("Could not create raycast blocker: " + ex.Message);
			}
		}

		private void OnDestroy()
		{
			if (_raycastBlocker != null)
			{
				Destroy(_raycastBlocker);
				_raycastBlocker = null;
			}
		}

		private void Update()
		{
			// StaticGameCoreAccessor.G is assigned when a game session is fully
			// initialized and reset to null when the session is disposed.
			object session = StaticGameCoreAccessor.G;
			if (!ReferenceEquals(session, _hookedSession))
			{
				_hookedSession = session;
				_simManager = session is IGameSessionManagers managers ? managers.SimulationSpeed : null;

				if (_simManager != null)
				{
					_selectedIndex = 1;
					ApplySpeed(Speeds[_selectedIndex], log: false);
					FastForwarderMod.LogInfo("SimulationSpeedManager hooked.");
				}
				else
				{
					FastForwarderMod.LogInfo("Game session closed; speed control idle.");
				}
			}

			// Keep the highlight in sync with the game's own pause toggle.
			SyncSelectionFromGame();

			// Hide the toolbar while the session is being torn down.
			bool gameState = _simManager != null;
			if (gameState != _lastGameState)
			{
				_lastGameState = gameState;

				if (gameState && _raycastBlocker == null)
				{
					try
					{
						CreateRaycastBlocker();
					}
					catch (Exception ex)
					{
						FastForwarderMod.LogWarning("Could not recreate raycast blocker: " + ex.Message);
					}
				}

				SetBlockerVisible(gameState);
			}
		}

		private void OnGUI()
		{
			if (_simManager == null)
			{
				return;
			}

			if (_buttonStyle == null)
			{
				_buttonStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 14,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter
				};
				_buttonStyle.normal.textColor = Color.white;
				_buttonStyle.hover.textColor = Color.white;
				_buttonStyle.active.textColor = Color.white;
			}

			float panelX = Screen.width - PanelWidth - PanelRightMargin;
			Rect panel = new Rect(panelX, PanelTop, PanelWidth, PanelHeight);

			GUI.BeginGroup(panel);

			_hoveredIndex = -1;

			for (int i = 0; i < Speeds.Length; i++)
			{
				Rect buttonRect = new Rect(i * 38f, 0f, 35f, 30f);
				GUI.backgroundColor = (i == _selectedIndex)
					? new Color(1f, 0.55f, 0.1f)
					: new Color(0.3f, 0.3f, 0.3f);

				if (buttonRect.Contains(Event.current.mousePosition))
				{
					_hoveredIndex = i;
				}

				if (GUI.Button(buttonRect, Labels[i], _buttonStyle))
				{
					_selectedIndex = i;
					ApplySpeed(Speeds[i], log: true);
				}
			}

			GUI.EndGroup();
			GUI.backgroundColor = Color.white;

			if (panel.Contains(Event.current.mousePosition))
			{
				Event current = Event.current;
				if (current.type == EventType.MouseDown
					|| current.type == EventType.MouseUp
					|| current.type == EventType.MouseDrag
					|| current.type == EventType.ScrollWheel)
				{
					current.Use();
				}

				if (_hoveredIndex >= 0 && _hoveredIndex < Tooltips.Length)
				{
					GUI.Label(new Rect(panelX - 100f, PanelTop + 34f, 300f, 20f), Tooltips[_hoveredIndex]);
				}
			}
		}

		private void SyncSelectionFromGame()
		{
			if (_simManager == null)
			{
				return;
			}

			float speed = _simManager.Speed;
			for (int i = 0; i < Speeds.Length; i++)
			{
				if (Mathf.Abs(speed - Speeds[i]) < 0.001f)
				{
					_selectedIndex = i;
					return;
				}
			}
		}

		private void ApplySpeed(float speed, bool log)
		{
			if (_simManager == null)
			{
				return;
			}

			_simManager.IsPaused = speed == 0f;
			_simManager.Speed = speed;

			if (log)
			{
				FastForwarderMod.LogInfo("Speed set to " + speed + "x");
			}
		}

		private void CreateRaycastBlocker()
		{
			_raycastBlocker = new GameObject("FastForwarder_RaycastBlocker");
			DontDestroyOnLoad(_raycastBlocker);
			_raycastBlocker.layer = LayerMask.NameToLayer("UI");

			Canvas canvas = _raycastBlocker.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 30000;
			_raycastBlocker.AddComponent<GraphicRaycaster>();

			GameObject imageObject = new GameObject("BlockerImage");
			imageObject.transform.SetParent(_raycastBlocker.transform, false);
			imageObject.layer = LayerMask.NameToLayer("UI");

			Image image = imageObject.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0.001f);

			RectTransform rectTransform = image.GetComponent<RectTransform>();
			rectTransform.anchorMin = new Vector2(1f, 1f);
			rectTransform.anchorMax = new Vector2(1f, 1f);
			rectTransform.pivot = new Vector2(1f, 1f);
			rectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
			rectTransform.anchoredPosition = new Vector2(-PanelRightMargin, -PanelTop);
		}

		private void SetBlockerVisible(bool visible)
		{
			if (_raycastBlocker != null)
			{
				_raycastBlocker.SetActive(visible);
			}
		}
	}
}
