using UnityEngine;
using TMPro;

// Teclado virtual clicável pra digitar em VR, já que o teclado nativo do Android não aparece
// dentro de um app imersivo OpenXR. Fica direto no prefab do teclado (ex.: Assets/Prefabs/Keyboard.prefab),
// já posicionado como instância fixa na cena - assim os botões de dentro do prefab conseguem
// referenciar esse componente no OnClick() deles (uma referência de cena não pode ser linkada
// de dentro de um prefab, só algo que já more no próprio prefab).
public class VirtualKeyboard : MonoBehaviour
{
    [Header("Posicionamento em VR")]
    [Tooltip("Se true, o teclado acompanha a câmera enquanto estiver aberto, em vez de ficar fixo onde foi colocado na cena.")]
    public bool positionInFrontOfCamera = false;
    public float distanceFromCamera = 1.2f;

    // Disparado só pelo botão "Confirmar" explícito - não pelo onEndEdit nativo do TMP_InputField,
    // que dispara sozinho toda vez que o campo perde o foco (ex.: ao clicar numa tecla), o que
    // salvaria o nome pela metade a cada clique se algo escutasse o onEndEdit diretamente.
    public event System.Action<string> Confirmed;

    private TMP_InputField target;
    private Transform cam;
    private bool isUpperCase = true;

    void Awake()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        gameObject.SetActive(false);
    }

    // Chama isso ao selecionar um InputField (ex.: via onSelect dele) em vez de deixar o
    // TMP_InputField tentar abrir o teclado nativo do Android sozinho (que fica invisível em VR).
    public void Open(TMP_InputField inputField)
    {
        target = inputField;

        if (target != null)
            target.shouldHideSoftKeyboard = true;

        gameObject.SetActive(true);
        PositionInFrontOfCamera();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // Confirma o texto (dispara Confirmed, ex.: o GameOverUI usa isso pra salvar no ranking)
    // e fecha o teclado. Liga no botão "Enter"/"OK".
    public void Confirm()
    {
        Confirmed?.Invoke(target != null ? target.text : string.Empty);
        Close();
    }

    // Liga no OnClick() de cada tecla de letra/número/símbolo. No Inspector do Button, em
    // On Click(), escolhe VirtualKeyboard.TypeCharacter(string) e digita o caractere daquela
    // tecla no campo de argumento (ex.: "a", "b", "5", "!").
    public void TypeCharacter(string character)
    {
        if (target == null || string.IsNullOrEmpty(character)) return;
        if (target.characterLimit > 0 && target.text.Length >= target.characterLimit) return;

        target.text += isUpperCase ? character.ToUpperInvariant() : character.ToLowerInvariant();
        RefocusTarget();
    }

    public void Space()
    {
        if (target == null) return;
        if (target.characterLimit > 0 && target.text.Length >= target.characterLimit) return;

        target.text += " ";
        RefocusTarget();
    }

    public void Backspace()
    {
        if (target == null || target.text.Length == 0) return;

        target.text = target.text.Substring(0, target.text.Length - 1);
        RefocusTarget();
    }

    public void Clear()
    {
        if (target == null) return;

        target.text = string.Empty;
        RefocusTarget();
    }

    // Clicar num botão do teclado tira o foco do InputField (comportamento normal de UI, o clique
    // seleciona o botão) - isso é só cosmético (caret/cursor), já que escrevemos direto em
    // target.text, mas reativar deixa o campo com cara de "ainda editando" entre uma tecla e outra.
    void RefocusTarget()
    {
        if (target == null) return;

        target.ActivateInputField();
        target.caretPosition = target.text.Length;
    }

    // Liga no botão de Shift/Caps Lock, se quiser um.
    public void ToggleCase()
    {
        isUpperCase = !isUpperCase;
    }

    void PositionInFrontOfCamera()
    {
        if (!positionInFrontOfCamera || cam == null) return;

        transform.position = cam.position + cam.forward * distanceFromCamera;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }

    void Update()
    {
        if (!positionInFrontOfCamera || cam == null) return;

        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
