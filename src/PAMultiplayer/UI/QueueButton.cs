using PAMultiplayer.Managers;
using TMPro;
using UnityEngine;

namespace PAMultiplayer.UI;

public class QueueButton : MonoBehaviour
{
    public UI_Button UIQueueButton { get; private set; }
    private TextMeshProUGUI queueText;

    private string currentLevel = "";
    private string currentLevelName = "MISSING";
    private int queueIndex = 0;
    private delegate void QueueUpdated();
    private static event QueueUpdated _queueUpdated;
    
    private void Start()
    {
        _queueUpdated += UpdateButton;
    }

    private void OnDestroy()
    {
        _queueUpdated -= UpdateButton;
    }

    void UpdateButton()
    {
        int newIndex = GlobalsManager.Queue.IndexOfLevel(currentLevel) + 1;
        if (newIndex > 0 && newIndex != queueIndex)
        {
            queueIndex = newIndex;
            UIQueueButton.Show();
            queueText.text = newIndex.ToString();
        }
    }
    public void OnClick()
    {
        if (GlobalsManager.Queue.ContainsLevel(currentLevel))
        {
            GlobalsManager.Queue.RemoveLevel(currentLevel);
            queueIndex = 0;
            queueText.text = "+";
            _queueUpdated?.Invoke();
        }
        else
        {
            GlobalsManager.Queue.AddLevel(currentLevelName, currentLevel);
            queueIndex = GlobalsManager.Queue.Count;
            queueText.text = queueIndex.ToString();
        }
    }

    public void SetLevel(string levelId, string name)
    {
        if (UIQueueButton == null)
        {
            UIQueueButton = gameObject.GetComponent<UI_Button>();
            queueText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
            UIQueueButton.GetComponent<MultiElementButton>().onClick.AddListener(OnClick);
        }

        UIQueueButton = gameObject.GetComponent<UI_Button>();
        UIQueueButton.Show();
        
        currentLevel = levelId;
        currentLevelName = name;
        queueIndex = GlobalsManager.Queue.IndexOfLevel(levelId) + 1;
        queueText.text = queueIndex > 0 ? queueIndex.ToString() : "+";
    }
}
