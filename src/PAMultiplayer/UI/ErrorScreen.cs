using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PAMultiplayer.UI;

public class ErrorScreen : MonoBehaviour
{
    private UI_Menu _menu;
    private TextMeshProUGUI _text;
    private MultiElementButton _button;
    private string _errorMessage;
    private GameObject _last;
    
    public static void CreateErrorScreen(string errorMessage)
    {
        Transform uiManager = PauseUIManager.Inst.transform.parent;
        var newError = Instantiate(PAM.ErrorScreenPrefab, uiManager);
        newError.transform.SetSiblingIndex(0);
        var component = newError.AddComponent<ErrorScreen>();
        component._errorMessage = errorMessage;
    }

    private void Start()
    {
        _text.text = _errorMessage;
        UIStateManager.Inst.RefreshTextCache(_text, _errorMessage);

        _last = EventSystem.current.currentSelectedGameObject;
        _menu.ShowBase();
        _menu.SwapView("main");
        _menu.AllViews["main"].PossibleFirstButtons[0].Select();
    }

    private void Awake()
    {
        _menu = GetComponent<UI_Menu>();
        _text = transform.Find("ErrorMessage").GetComponent<TextMeshProUGUI>();
        _button = transform.Find("Close/Close").GetComponent<MultiElementButton>();
        _button.onClick.AddListener(Close);
    }

    private void Close()
    {
        _menu.HideAll();
        Destroy(gameObject, 1);
        
        if (_last)
        {
            EventSystem.current.SetSelectedGameObject(_last);
        }
    }
}