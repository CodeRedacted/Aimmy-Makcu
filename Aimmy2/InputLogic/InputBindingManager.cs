using Gma.System.MouseKeyHook;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using MouseMovementLibraries.MakcuSupport;

namespace InputLogic
{
    internal class InputBindingManager
    {
        private IKeyboardMouseEvents? _mEvents;
        private readonly Dictionary<string, string> bindings = new();
        private static readonly Dictionary<string, bool> isHolding = new();
        private string? settingBindingId = null;

        public event Action<string, string>? OnBindingSet;
        public event Action<string>? OnBindingPressed;
        public event Action<string>? OnBindingReleased;

        public static bool IsHoldingBinding(string bindingId) =>
            isHolding.TryGetValue(bindingId, out bool holding) && holding;

        public void SetupDefault(string bindingId, string keyCode)
        {
            bindings[bindingId] = keyCode;
            isHolding[bindingId] = false;
            OnBindingSet?.Invoke(bindingId, keyCode);
            EnsureHookEvents();
        }

        public void StartListeningForBinding(string bindingId)
        {
            settingBindingId = bindingId;
            EnsureHookEvents();
        }

        private void EnsureHookEvents()
        {
            if (_mEvents == null)
            {
                _mEvents = Hook.GlobalEvents();
                _mEvents.KeyDown += GlobalHookKeyDown!;
                _mEvents.MouseDown += GlobalHookMouseDown!;
                _mEvents.KeyUp += GlobalHookKeyUp!;
                _mEvents.MouseUp += GlobalHookMouseUp!;
            }
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            string keyCodeStr = e.KeyCode.ToString();
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = keyCodeStr;
                isHolding[settingBindingId] = false;
                OnBindingSet?.Invoke(settingBindingId, keyCodeStr);
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == keyCodeStr)
                    {
                        isHolding[binding.Key] = true;
                        OnBindingPressed?.Invoke(binding.Key);
                    }
                }
            }
        }

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            string buttonCodeStr = e.Button.ToString();
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = buttonCodeStr;
                isHolding[settingBindingId] = false;
                OnBindingSet?.Invoke(settingBindingId, buttonCodeStr);
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == buttonCodeStr)
                    {
                        isHolding[binding.Key] = true;
                        OnBindingPressed?.Invoke(binding.Key);
                    }
                }
            }
        }

        private void GlobalHookKeyUp(object sender, KeyEventArgs e)
        {
            string keyCodeStr = e.KeyCode.ToString();
            foreach (var binding in bindings)
            {
                if (binding.Value == keyCodeStr)
                {
                    isHolding[binding.Key] = false;
                    OnBindingReleased?.Invoke(binding.Key);
                }
            }
        }

        private void GlobalHookMouseUp(object sender, MouseEventArgs e)
        {
            string buttonCodeStr = e.Button.ToString();
            foreach (var binding in bindings)
            {
                if (binding.Value == buttonCodeStr)
                {
                    isHolding[binding.Key] = false;
                    OnBindingReleased?.Invoke(binding.Key);
                }
            }
        }

        public void StopListening()
        {
            if (_mEvents != null)
            {
                _mEvents.KeyDown -= GlobalHookKeyDown!;
                _mEvents.MouseDown -= GlobalHookMouseDown!;
                _mEvents.KeyUp -= GlobalHookKeyUp!;
                _mEvents.MouseUp -= GlobalHookMouseUp!;
                _mEvents.Dispose();
                _mEvents = null;
            }
        }

        // ---------------- Makcu Integration ----------------

        public void HandleMakcuButton(MakcuMouseButton button, bool isPressed)
        {
            string makcuCode = $"Makcu_{button}";

            if (settingBindingId != null && isPressed)
            {
                bindings[settingBindingId] = makcuCode;
                isHolding[settingBindingId] = false;
                OnBindingSet?.Invoke(settingBindingId, makcuCode);
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == makcuCode)
                    {
                        isHolding[binding.Key] = isPressed;
                        if (isPressed)
                            OnBindingPressed?.Invoke(binding.Key);
                        else
                            OnBindingReleased?.Invoke(binding.Key);
                    }
                }
            }
        }
    }
}
