using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Janela de Editor pra criar ItemDefinition/UpgradeDefinition rapidamente (GDD 2, seção 9) sem
// passar pelo menu Create > preencher campo por campo > arrastar referência uma por uma. Abre em
// Dungeon > Ferramenta de Conteúdo da Loja. Cada aba já salva o asset na pasta certa
// (Assets/SO/Item Definitions ou Assets/SO/Upgrades/<Raridade>), criando a pasta se precisar.
public class ShopContentToolWindow : EditorWindow
{
    const string ItemFolder = "Assets/SO/Item Definitions";
    const string UpgradeFolderRoot = "Assets/SO/Upgrades";
    const string ProjectilePrefabFolder = "Assets/Prefabs";
    const string TemplatePrefabPath = "Assets/Prefabs/Projectile_Bomb.prefab";
    const string IconFolder = "Assets/SO/Item Definitions/Icons";

    int tab;
    readonly string[] tabs = { "Itens", "Upgrades", "Projétil" };
    Vector2 scroll;

    // --- aba Itens ---
    GameObject itemPrefab;
    string itemName = "";
    string itemDescription = "";
    Sprite itemIcon;
    bool itemUnlimitedStock;
    int itemCost = 10;
    int itemStockPerPurchase = 3;
    string itemStockLabel = "unidades";

    // --- aba Upgrades ---
    string upgradeName = "";
    string upgradeDescription = "";
    ItemRarity upgradeRarity = ItemRarity.Comum;
    int upgradeTargetIndex;
    int upgradeDamageBonus = 5;
    int upgradeCost = 15;

    ItemDefinition[] cachedItems;

    // --- aba Projétil ---
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
        tab = GUILayout.Toolbar(tab, tabs);
        EditorGUILayout.Space(10);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawItemTab();
        else if (tab == 1) DrawUpgradeTab();
        else DrawProjectileTab();
        EditorGUILayout.EndScrollView();
    }

    // ---------- ITENS ----------

    void DrawItemTab()
    {
        EditorGUILayout.LabelField("Novo Item", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arraste o prefab do projétil (precisa implementar IThrowable) - o nome é sugerido a partir do nome do prefab. Vira 1 variante com 100% de chance.", MessageType.None);

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

        itemPrefab = null;
        itemName = "";
        itemDescription = "";
        itemIcon = null;
        RefreshItemCache();
    }

    // ---------- UPGRADES ----------

    void DrawUpgradeTab()
    {
        EditorGUILayout.LabelField("Novo Upgrade", EditorStyles.boldLabel);

        if (cachedItems == null || cachedItems.Length == 0)
        {
            EditorGUILayout.HelpBox("Nenhum ItemDefinition encontrado no projeto ainda - crie itens na aba 'Itens' primeiro (um upgrade sempre precisa de um item-alvo).", MessageType.Info);
            if (GUILayout.Button("Recarregar")) RefreshItemCache();
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

        upgradeName = "";
        upgradeDescription = "";
    }

    // ---------- PROJÉTIL ----------

    void DrawProjectileTab()
    {
        if (!projTemplateLoaded)
        {
            LoadProjectileTemplateDefaults();
            projTemplateLoaded = true;
        }

        EditorGUILayout.LabelField("Novo Prefab de Projétil", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arraste o modelo 3D (prefab/FBX da MinionsArt etc.) - crio um prefab com a mesma estrutura do Projectile_Bomb (raiz com Rigidbody + Projectile_Bomb, filho 'Visual' com o modelo, filho 'Collider' com uma SphereCollider já ajustada ao tamanho do modelo). Ao terminar, já pulo pra aba Itens com esse prefab preenchido.", MessageType.None);

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
    }

    // Puxa layer e explosão/escala padrão do Projectile_Bomb existente, só na primeira vez que a
    // aba abre - assim novo projétil já nasce com uma explosão de verdade em vez de nada.
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

        // encadeia direto pra aba Itens, já com o prefab novo preenchido - só falta custo/estoque.
        itemPrefab = savedPrefab;
        itemName = projName;
        itemIcon = null;
        tab = 0;
        Repaint();

        // gera o ícone a partir do preview do prefab recém-criado, em paralelo (assíncrono) -
        // quando terminar já preenche o campo Ícone da aba Itens sozinho.
        GenerateIconFromPreview(savedPrefab, sprite =>
        {
            itemIcon = sprite;
            Repaint();
        });

        projModel = null;
        projName = "";
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
