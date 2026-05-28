using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonKeyTrigger : MonoBehaviour
{
    public enum TriggerKey { PageUp, PageDown, AnyKey, AnyKeyExceptPaging }

    public TriggerKey key = TriggerKey.PageDown;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Update()
    {
        bool pressed = key switch
        {
            TriggerKey.PageUp             => Input.GetKeyDown(KeyCode.PageUp),
            TriggerKey.PageDown           => Input.GetKeyDown(KeyCode.PageDown),
            TriggerKey.AnyKey             => Input.anyKeyDown,
            TriggerKey.AnyKeyExceptPaging => Input.anyKeyDown
                                             && !Input.GetKeyDown(KeyCode.PageUp)
                                             && !Input.GetKeyDown(KeyCode.PageDown),
            _                             => false,
        };

        if (pressed && button.interactable)
            button.onClick.Invoke();
    }
}
