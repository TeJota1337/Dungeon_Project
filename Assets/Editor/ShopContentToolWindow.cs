using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assistente guiado (wizard) de 3 etapas pra criar conteúdo da loja (GDD 2, seção 9): Projétil ->
// Item -> Upgrade. Abre em Dungeon > Ferramenta de Conteúdo da Loja.
//
// Como funciona a navegação:
//  - A "zona inteligente" no topo aceita QUALQUER coisa arrastada (modelo 3D, prefab de projétil,
//    ItemDefinition) e detecta sozinha em qual etapa você deveria estar, pulando pra lá.
//  - O indicador de etapas embaixo dela também é clicável - dá pra pular manualmente a qualquer
//    momento, pra frente ou pra trás.
//  - Depois de criar algo, a etapa mostra um aviso com botões: seguir pra próxima etapa (já com
//    o que acabou de criar pré-preenchido) ou criar outro do mesmo tipo.
// Cada etapa salva o asset na pasta certa (Assets/SO/Item Definitions, Assets/SO/Upgrades/<Raridade>
// ou Assets/Prefabs), criando a pasta se precisar.
public class ShopContentToolWindow : EditorWindow
{
    const string ItemFolder = "Assets/SO/Item Definitions";
    const string UpgradeFolderRoot = "Assets/SO/Upgrades";
    const string ProjectilePrefabFolder = "Assets/Prefabs";
    const string TemplatePrefabPath = "Assets/Prefabs/Projectile_Bomb.prefab";
    const string IconFolder = "Assets/SO/Item Definitions/Icons";

    enum Step { Projectile, Item, Upgrade }
    Step currentStep = Step.Projectile;
    Vector2 scroll;

    // --- etapa Item ---
    GameObject itemPrefab;
    string itemName = "";
    string itemDescription = "";
    Sprite itemIcon;
    bool itemUnlimitedStock;
    int itemCost = 10;
    int itemStockPerPurchase = 3;
    string itemStockLabel = "unidades";
    bool itemCreated;
    ItemDefinition lastItem;

    // --- etapa Upgrade ---
    string upgradeName = "";
    string upgradeDescription = "";
    ItemRarity upgradeRarity = ItemRarity.Comum;
    int upgradeTargetIndex;
    int upgradeDamageBonus = 5;
    int upgradeCost = 15;
    bool upgradeCreated;
    string lastUpgradeName;

    ItemDefinition[] cachedItems;

    // --- etapa Projétil ---
    GameObject projModel;
    string projName = "";
    float projScale = 1f;
    int projMinDamage = 5;
    int projMaxDamage = 15;
    float projSplashRadius = 1.5f;
    float projSplashDamageMultiplier = 0.5f;
    float projDestroyAfterSeconds = 5f;
    GameObject projExplosionPrefab;
    float projExplosionScaleMultiplier = 1f;
    bool projTemplateLoaded;
    bool projectileCreated;
    string lastProjectileName;

    [MenuItem("Dungeon/Ferramenta de Conteúdo da Loja")]
    static void Open()
    {
        GetWindow<ShopContentToolWindow>("Conteúdo da Loja");
    }

    void OnEnable()
    {
        RefreshItemCache();
    }

    void RefreshItemCache()
    {
        cachedItems = AssetDatabase.FindAssets("t:ItemDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(item => item != null)
            .OrderBy(item => item.itemName)
            .ToArray();
    }

    void OnGUI()
    {
        DrawSmartDropZone();
        EditorGUILayout.Space(6);
        DrawStepIndicator();
        EditorGUILayout.Space(10);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (currentStep)
        {
            case Step.Projectile: DrawProjectileStep(); break;
            case Step.Item: DrawItemStep(); break;
            case Step.Upgrade: DrawUpgradeStep(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    // ---------- NAVEGAÇÃO: zona inteligente + indicador de etapas ----------

    void DrawSmartDropZone()
    {
        Rect dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Comece por aqui: arraste um modelo 3D, um Prefab de projétil ou um Item já existente\n(a ferramenta detecta e pula pra etapa certa sozinha)");

        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                RouteDroppedObject(obj);
            evt.Use();
        }
    }

    // Decide em qual etapa o objeto solto se encaixa: ItemDefinition -> Upgrade; prefab que já
    // implementa IThrowable -> Item; qualquer outro GameObject/modelo -> Projétil.
    void RouteDroppedObject(Object obj)
    {
        if (obj is ItemDefinition item)
        {
            int idx = System.Array.IndexOf(cachedItems, item);
            if (idx >= 0) upgradeTargetIndex = idx;
            itemCreated = false;
            currentStep = Step.Upgrade;
            Debug.Log($"Ferramenta de Conteúdo: '{item.itemName}' é um Item - indo pra etapa de Upgrade.");
            return;
        }

        if (obj is GameObject go)
        {
            if (go.GetComponent<IThrowable>() != null)
            {
                itemPrefab = go;
                itemName = go.name.Replace("Projectile_", "");
                projectileCreated = false;
                currentStep = Step.Item;
                Debug.Log($"Ferramenta de Conteúdo: '{go.name}' já implementa IThrowable - indo pra etapa de Item.");
            }
            else
            {
                projModel = go;
                projName = go.name;
                currentStep = Step.Projectile;
                Debug.Log($"Ferramenta de Conteúdo: '{go.name}' reconhecido como modelo 3D - indo pra etapa de Projétil.");
            }
            return;
        }

        Debug.LogWarning($"Ferramenta de Conteúdo: não reconheço '{obj.name}' - arraste um modelo 3D, um Prefab de projétil (IThrowable) ou um ItemDefinition.");
    }

    void DrawStepIndicator()
    {
        EditorGUILayout.BeginHorizontal();
        DrawStepButton("① Projétil", Step.Projectile);
        GUILayout.Label("→", GUILayout.Width(18));
        DrawStepButton("② Item", Step.Item);
        GUILayout.Label("→", GUILayout.Width(18));
        DrawStepButton("③ Upgrade", Step.Upgrade);
        EditorGUILayout.EndHorizontal();
    }

    void DrawStepButton(string label, Step step)
    {
        Color previous = GUI.backgroundColor;
        if (currentStep == step) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);

        if (GUILayout.Button(label, GUILayout.Height(26)))
            currentStep = step;

        GUI.backgroundColor = previous;
    }

    // ---------- ETAPA 1: PROJÉTIL ----------

    void DrawProjectileStep()
    {
        if (!projTemplateLoaded)
        {
            LoadProjectileTemplateDefaults();
            projTemplateLoaded = true;
        }

        if (projectileCreated)
        {
            EditorGUILayout.HelpBox($"✓ Prefab '{lastProjectileName}' criado em {ProjectilePrefabFolder}/.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Criar Item com esse prefab →", GUILayout.Height(26)))
            {
                projectileCreated = false;
                currentStep = Step.Item;
            }
            if (GUILayout.Button("Criar outro Projétil", GUILayout.Height(26)))
                projectileCreated = false;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);
        }

        EditorGUILayout.LabelField("Etapa 1 — Prefab do Projétil", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ponto de partida se você só tem o modelo 3D ainda. Arraste ele aqui (prefab/FBX) e a ferramenta monta um prefab completo: raiz com Rigidbody + Projectile_Bomb, filho 'Visual' com o modelo, filho 'Collider' com uma SphereCollider já ajustada ao tamanho real do modelo. Se você já tem um prefab de projétil pronto, pode pular direto pra Etapa 2.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        projModel = (GameObject)EditorGUILayout.ObjectField("Modelo 3D", projModel, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && projModel != null && string.IsNullOrEmpty(projName))
            projName = projModel.name;

        projName = EditorGUILayout.TextField("Nome do Projétil", projName);
        projScale = EditorGUILayout.FloatField(new GUIContent("Escala (raiz)", "Escala aplicada no objeto raiz do prefab - o Collider é calculado já levando essa escala em conta."), projScale);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Dano", EditorStyles.miniBoldLabel);
        projMinDamage = EditorGUILayout.IntField("Dano Mínimo", projMinDamage);
        projMaxDamage = EditorGUILayout.IntField("Dano Máximo", projMaxDamage);
        projSplashRadius = EditorGUILayout.FloatField("Raio de Splash", projSplashRadius);
        projSplashDamageMultiplier = EditorGUILayout.Slider("Multiplicador de Splash", projSplashDamageMultiplier, 0f, 1f);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Explosão", EditorStyles.miniBoldLabel);
        projExplosionPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab de Explosão", projExplosionPrefab, typeof(GameObject), false);
        projExplosionScaleMultiplier = EditorGUILayout.FloatField("Escala da Explosão", projExplosionScaleMultiplier);
        projDestroyAfterSeconds = EditorGUILayout.FloatField("Autodestruição (s)", projDestroyAfterSeconds);

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(projModel == null || string.IsNullOrWhiteSpace(projName)))
        {
            if (GUILayout.Button("Criar Prefab do Projétil", GUILayout.Height(28)))
                CreateProjectilePrefab();
        }

        EditorGUILayout.Space(14);
        EditorGUILayout.HelpBox("Já tem um prefab pronto (implementa IThrowable, ex: Bomb, Stone)? Não precisa passar por aqui - vá direto pra Etapa 2 (② Item) ou arraste ele na zona inteligente no topo.", MessageType.None);
    }

    // Puxa layer e explosão/escala padrão do Projectile_Bomb existente, só na primeira vez que a
    // etapa abre - assim novo projétil já nasce com uma explosão de verdade em vez de nada.
    void LoadProjectileTemplateDefaults()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null) return;

        Projectile_Bomb bomb = template.GetComponent<Projectile_Bomb>();
        if (bomb == null) return;

        if (projExplosionPrefab == null) projExplosionPrefab = bomb.explosionPrefab;
        projExplosionScaleMultiplier = bomb.explosionScaleMultiplier;
    }

    void CreateProjectilePrefab()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        int layer = template != null ? template.layer : 0;

        GameObject root = new GameObject($"Projectile_{projName}");
        root.layer = layer;
        root.transform.localScale = Vector3.one * Mathf.Max(projScale, 0.0001f);

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;

        Projectile_Bomb proj = root.AddComponent<Projectile_Bomb>();
        proj.minDamage = projMinDamage;
        proj.maxDamage = projMaxDamage;
        proj.splashRadius = projSplashRadius;
        proj.splashDamageMultiplier = projSplashDamageMultiplier;
        proj.destroyAfterSeconds = projDestroyAfterSeconds;
        proj.explosionPrefab = projExplosionPrefab;
        proj.explosionScaleMultiplier = projExplosionScaleMultiplier;

        GameObject visual = new GameObject("Visual");
        visual.layer = layer;
        visual.transform.SetParent(root.transform, false);

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(projModel) as GameObject;
        if (modelInstance == null) modelInstance = Instantiate(projModel);
        modelInstance.transform.SetParent(visual.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        GameObject colliderObj = new GameObject("Collider");
        colliderObj.layer = layer;
        colliderObj.transform.SetParent(root.transform, false);
        SphereCollider sphere = colliderObj.AddComponent<SphereCollider>();

        // ajusta o Collider ao tamanho real do modelo em vez de deixar um raio padrão chutado.
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            sphere.center = root.transform.InverseTransformPoint(bounds.center);
            sphere.radius = bounds.extents.magnitude / root.transform.localScale.x;
        }
        else
        {
            Debug.LogWarning($"Ferramenta de Conteúdo: '{projModel.name}' não tem Renderer nos filhos - deixei o Collider com raio padrão (0.5), ajusta na mão.");
            sphere.radius = 0.5f;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{ProjectilePrefabFolder}/Projectile_{projName}.prefab");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log($"Ferramenta de Conteúdo: criei '{path}'.");
        EditorGUIUtility.PingObject(savedPrefab);

        // pré-preenche a etapa Item (caso o usuário escolha avançar) e já dispara a geração do
        // ícone em paralelo - quando terminar, preenche o campo Ícone sozinho.
        itemPrefab = savedPrefab;
        itemName = projName;
        itemIcon = null;
        GenerateIconFromPreview(savedPrefab, sprite =>
        {
            itemIcon = sprite;
            Repaint();
        });

        lastProjectileName = projName;
        projectileCreated = true;

        projModel = null;
        projName = "";
        Repaint();
    }

    // ---------- ETAPA 2: ITEM ----------

    void DrawItemStep()
    {
        if (itemCreated)
        {
            EditorGUILayout.HelpBox($"✓ Item '{lastItem.itemName}' criado.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Criar Upgrade pra esse Item →", GUILayout.Height(26)))
            {
                int idx = System.Array.IndexOf(cachedItems, lastItem);
                if (idx >= 0) upgradeTargetIndex = idx;
                itemCreated = false;
                currentStep = Step.Upgrade;
            }
            if (GUILayout.Button("Criar outro Item", GUILayout.Height(26)))
                itemCreated = false;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);
        }

        EditorGUILayout.LabelField("Etapa 2 — Item da Loja", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Transforma um prefab de projétil num item comprável na loja. Arraste o prefab (precisa implementar IThrowable - a Etapa 1 já garante isso, ou use um pronto como Bomb/Stone). Vira 1 variante com 100% de chance.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        itemPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab do Projétil", itemPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && itemPrefab != null && string.IsNullOrEmpty(itemName))
            itemName = itemPrefab.name.Replace("Projectile_", "");

        if (itemPrefab != null && itemPrefab.GetComponent<IThrowable>() == null)
            EditorGUILayout.HelpBox("Esse prefab não implementa IThrowable - o estilingue não vai conseguir lançar ele.", MessageType.Warning);

        itemName = EditorGUILayout.TextField("Nome", itemName);
        EditorGUILayout.LabelField("Descrição");
        itemDescription = EditorGUILayout.TextArea(itemDescription, GUILayout.Height(40));

        EditorGUILayout.BeginHorizontal();
        itemIcon = (Sprite)EditorGUILayout.ObjectField("Ícone", itemIcon, typeof(Sprite), false);
        using (new EditorGUI.DisabledScope(itemPrefab == null))
        {
            if (GUILayout.Button("Gerar do preview", GUILayout.Width(110)))
            {
                GenerateIconFromPreview(itemPrefab, sprite =>
                {
                    itemIcon = sprite;
                    Repaint();
                });
            }
        }
        EditorGUILayout.EndHorizontal();

        itemUnlimitedStock = EditorGUILayout.Toggle(new GUIContent("Estoque Ilimitado", "Ex: a pedra - ignora custo e estoque."), itemUnlimitedStock);
        using (new EditorGUI.DisabledScope(itemUnlimitedStock))
        {
            itemCost = EditorGUILayout.IntField("Custo", itemCost);
            itemStockPerPurchase = EditorGUILayout.IntField("Qtd por Compra", itemStockPerPurchase);
            itemStockLabel = EditorGUILayout.TextField("Rótulo da Qtd", itemStockLabel);
        }

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(itemPrefab == null || string.IsNullOrWhiteSpace(itemName)))
        {
            if (GUILayout.Button("Criar Item", GUILayout.Height(28)))
                CreateItem();
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(cachedItems == null || cachedItems.Length == 0))
        {
            if (GUILayout.Button("Gerar ícones faltantes nos itens existentes"))
                GenerateMissingIcons();
        }

        EditorGUILayout.Space(14);
        EditorGUILayout.HelpBox("Já tem um Item pronto e só quer criar um Upgrade pra ele? Vá direto pra Etapa 3 (③ Upgrade) ou arraste o ItemDefinition na zona inteligente no topo.", MessageType.None);

        DrawExistingList("Itens existentes", cachedItems?.Select(i => i.itemName).ToArray());
    }

    // Preenche o Ícone de todo ItemDefinition já existente que ainda não tem um, usando o preview
    // do prefab da primeira variante - pros itens criados antes desse recurso existir.
    void GenerateMissingIcons()
    {
        if (cachedItems == null) return;

        int count = 0;
        foreach (var item in cachedItems)
        {
            if (item == null || item.icon != null) continue;

            GameObject prefab = item.variants != null && item.variants.Length > 0 ? item.variants[0].prefab : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Ferramenta de Conteúdo: '{item.itemName}' não tem prefab na primeira variante - pulei.");
                continue;
            }

            count++;
            ItemDefinition target = item;
            GenerateIconFromPreview(prefab, sprite =>
            {
                if (sprite == null) return;

                target.icon = sprite;
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                Repaint();
            });
        }

        Debug.Log(count == 0
            ? "Ferramenta de Conteúdo: nenhum item sem ícone encontrado."
            : $"Ferramenta de Conteúdo: gerando ícone pra {count} item(ns) sem ícone...");
    }

    void CreateItem()
    {
        EnsureFolder(ItemFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{ItemFolder}/{itemName}.asset");

        var asset = CreateInstance<ItemDefinition>();
        asset.itemName = itemName;
        asset.description = itemDescription;
        asset.icon = itemIcon;
        asset.unlimitedStock = itemUnlimitedStock;
        asset.cost = itemCost;
        asset.stockPerPurchase = itemStockPerPurchase;
        asset.stockLabel = itemStockLabel;
        asset.variants = new[] { new ProjectileVariant { prefab = itemPrefab, chancePercent = 100f, previousPercent = 100f } };

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"Ferramenta de Conteúdo: criei '{path}'.");

        RefreshItemCache();
        lastItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        itemCreated = true;

        itemPrefab = null;
        itemName = "";
        itemDescription = "";
        itemIcon = null;
    }

    // ---------- ETAPA 3: UPGRADE ----------

    void DrawUpgradeStep()
    {
        if (upgradeCreated)
        {
            EditorGUILayout.HelpBox($"✓ Upgrade '{lastUpgradeName}' criado.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Criar outro Upgrade pro mesmo Item", GUILayout.Height(26)))
                upgradeCreated = false;
            if (GUILayout.Button("Concluir (voltar ao início)", GUILayout.Height(26)))
            {
                upgradeCreated = false;
                currentStep = Step.Projectile;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);
        }

        EditorGUILayout.LabelField("Etapa 3 — Upgrade", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Cria um bônus de dano sorteável na loja, mirando num Item já existente.", MessageType.None);

        if (cachedItems == null || cachedItems.Length == 0)
        {
            EditorGUILayout.HelpBox("Nenhum ItemDefinition encontrado no projeto ainda - um upgrade sempre precisa de um item-alvo.", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Recarregar")) RefreshItemCache();
            if (GUILayout.Button("Ir pra Etapa 2 — Item")) currentStep = Step.Item;
            EditorGUILayout.EndHorizontal();
            return;
        }

        upgradeName = EditorGUILayout.TextField("Nome", upgradeName);
        EditorGUILayout.LabelField("Descrição");
        upgradeDescription = EditorGUILayout.TextArea(upgradeDescription, GUILayout.Height(40));

        upgradeRarity = (ItemRarity)EditorGUILayout.EnumPopup(new GUIContent("Raridade", "Também decide em qual subpasta o asset é salvo."), upgradeRarity);

        string[] itemNames = cachedItems.Select(i => i.itemName).ToArray();
        upgradeTargetIndex = Mathf.Clamp(upgradeTargetIndex, 0, itemNames.Length - 1);
        upgradeTargetIndex = EditorGUILayout.Popup("Item-alvo", upgradeTargetIndex, itemNames);

        upgradeDamageBonus = EditorGUILayout.IntField("Bônus de Dano", upgradeDamageBonus);
        upgradeCost = EditorGUILayout.IntField("Custo", upgradeCost);

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(upgradeName)))
        {
            if (GUILayout.Button("Criar Upgrade", GUILayout.Height(28)))
                CreateUpgrade();
        }

        string folder = $"{UpgradeFolderRoot}/{upgradeRarity}";
        string[] existing = AssetDatabase.IsValidFolder(folder)
            ? AssetDatabase.FindAssets("t:UpgradeDefinition", new[] { folder })
                .Select(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray()
            : new string[0];
        DrawExistingList($"Upgrades existentes em {upgradeRarity}", existing);
    }

    void CreateUpgrade()
    {
        string folder = $"{UpgradeFolderRoot}/{upgradeRarity}";
        EnsureFolder(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{upgradeName}.asset");

        var asset = CreateInstance<UpgradeDefinition>();
        asset.upgradeName = upgradeName;
        asset.description = upgradeDescription;
        asset.rarity = upgradeRarity;
        asset.targetItem = cachedItems[upgradeTargetIndex];
        asset.damageBonus = upgradeDamageBonus;
        asset.cost = upgradeCost;

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"Ferramenta de Conteúdo: criei '{path}'.");

        lastUpgradeName = upgradeName;
        upgradeCreated = true;

        upgradeName = "";
        upgradeDescription = "";
    }

    // ---------- ÍCONE (preview do Editor assado em Sprite) ----------

    // Pega o mesmo thumbnail que a Unity já desenha pro prefab/modelo na janela Project e salva
    // como um Sprite de verdade dentro do projeto - evita ter que exportar/desenhar um ícone à
    // parte só pra loja. O preview é assíncrono (pode voltar null enquanto ainda tá renderizando),
    // então fica escutando EditorApplication.update até ele ficar pronto.
    void GenerateIconFromPreview(GameObject source, System.Action<Sprite> onReady)
    {
        if (source == null) { onReady(null); return; }

        void Poll()
        {
            Texture2D preview = AssetPreview.GetAssetPreview(source);
            if (preview == null && AssetPreview.IsLoadingAssetPreview(source.GetEntityId()))
                return; // ainda renderizando - tenta de novo no próximo tick do Editor

            EditorApplication.update -= Poll;

            if (preview == null)
            {
                Debug.LogWarning($"Ferramenta de Conteúdo: não consegui gerar preview de '{source.name}' - selecione o prefab no Project uma vez (pra Unity desenhar o thumbnail) e tente de novo.");
                onReady(null);
                return;
            }

            onReady(SavePreviewAsSprite(preview, source.name));
        }

        EditorApplication.update += Poll;
    }

    Sprite SavePreviewAsSprite(Texture2D preview, string baseName)
    {
        EnsureFolder(IconFolder);

        // copia os pixels pra uma textura nova, legível - a textura de preview em si não é um
        // asset salvável (é gerada em memória pelo Editor e pode sumir a qualquer momento).
        Texture2D copy = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        copy.SetPixels(preview.GetPixels());
        copy.Apply();
        byte[] png = copy.EncodeToPNG();
        DestroyImmediate(copy);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{IconFolder}/{baseName}_icon.png");
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"Ferramenta de Conteúdo: ícone gerado em '{path}'.");
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- COMPARTILHADO ----------

    static void DrawExistingList(string label, string[] names)
    {
        if (names == null || names.Length == 0) return;

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField($"{label} ({names.Length})", EditorStyles.boldLabel);
        foreach (var n in names.OrderBy(n => n))
            EditorGUILayout.LabelField("• " + n, EditorStyles.miniLabel);
    }

    // Cria a pasta (e os pais que faltarem) se ainda não existir - assim não precisa deixar
    // "Assets/SO/Upgrades/Mitico" pré-criada só pra poder usar a ferramenta.
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
