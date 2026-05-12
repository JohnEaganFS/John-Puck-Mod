using System;
using UnityEngine;
using UnityEngine.UIElements;

public class InServerRelaySelectionRunner : MonoBehaviour
{
    bool injected = false;
    float lastAttempt = 0f;

    void Update()
    {
        try
        {
            if (injected) return;
            if (Time.time - lastAttempt < 0.5f) return;
            lastAttempt = Time.time;

            var uiMgr = UIManager.Instance;
            if (uiMgr == null) return;
            var root = uiMgr.RootVisualElement;
            if (root == null) return;

            var container = root.Q("PauseMenu");
            if (container == null)
            {
                Debug.Log("[InServerRelaySelectionRunner] PauseMenu not found yet.");
                return;
            }

            // avoid duplicate by fixed element name
            var existing = container.Q<Button>("RelaySelectionButton");
            if (existing != null)
            {
                Debug.Log("[InServerRelaySelectionRunner] Relay button already present.");
                injected = true;
                Destroy(this.gameObject);
                return;
            }

            var relayBtn = new Button(() => { 
                try { JohnRelayMod.InServerRelaySelectionUI.ShowRelaySelectionForCurrentServer(); } catch (Exception e) { Debug.LogException(e); }
            }) { text = "Relay Selection" };
            relayBtn.name = "RelaySelectionButton";
            relayBtn.style.marginTop = 4;
            relayBtn.style.marginBottom = 4;
            relayBtn.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            relayBtn.style.color = new StyleColor(Color.white);
            relayBtn.style.borderTopWidth = 1;
            relayBtn.style.borderBottomWidth = 1;
            relayBtn.style.borderLeftWidth = 1;
            relayBtn.style.borderRightWidth = 1;
            relayBtn.style.borderTopColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            relayBtn.style.borderBottomColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            relayBtn.style.borderLeftColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            relayBtn.style.borderRightColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            relayBtn.style.paddingLeft = 6;
            relayBtn.style.paddingRight = 6;
            relayBtn.style.paddingTop = 2;
            relayBtn.style.paddingBottom = 2;

            container.Add(relayBtn);
            Debug.Log("[InServerRelaySelectionRunner] Injected Relay Selection button into pause menu (runtime fallback).");
            injected = true;
            Destroy(this.gameObject);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
