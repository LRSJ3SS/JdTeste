using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Linq;
using UABEA.Web.Models;

namespace UABEA.Web.Services
{
    // Equivalente web do par (AssetsManager + AssetWorkspace) do UABEAvalonia.
    // Diferença chave em relação ao original: em vez de abrir arquivos por
    // caminho no disco (System.IO.File.Open), tudo aqui entra como byte[]
    // vindo do <InputFile> do navegador e é lido via MemoryStream.
    public class AssetWorkspaceService
    {
        public AssetsManager Manager { get; } = new AssetsManager();
        private readonly HttpClient _http;

        public AssetsFileInstance? CurrentFile { get; private set; }
        public string? CurrentFileName { get; private set; }
        public List<AssetRow> Rows { get; } = new();

        public string? LastError { get; private set; }
        public string? TextureError { get; private set; }
        private bool _classPackageLoaded;

        public AssetWorkspaceService(HttpClient http)
        {
            _http = http;
        }

        // Baixa o classdata.tpk servido em wwwroot/classdata.tpk (via HTTP,
        // já que não existe "disco" no WASM) e carrega no AssetsManager.
        // NOTA PARA QUEM FOR COMPILAR: não tive como confirmar a assinatura
        // exata de LoadClassPackage nesta sessão (sem SDK .NET instalado
        // para inspecionar a DLL). A API pública da AssetsTools.NET expõe
        // overloads que aceitam Stream; se "LoadClassPackage(Stream)" não
        // existir na versão do pacote, troque por LoadClassPackage(byte[])
        // ou grave os bytes num arquivo temporário em memória (MemoryStream)
        // e ajuste a chamada — a lib é a mesma usada pelo app desktop.
        private async Task EnsureClassPackageLoadedAsync()
        {
            if (_classPackageLoaded) return;

            var bytes = await _http.GetByteArrayAsync("classdata.tpk");
            using var ms = new MemoryStream(bytes);
            Manager.LoadClassPackage(ms);
            _classPackageLoaded = true;
        }

        // Chamado quando o usuário seleciona um arquivo pelo <InputFile>.
        // fileBytes já deve estar carregado em memória (ver Pages/Home.razor).
        // Detecta automaticamente se é um .assets "solto" ou um AssetBundle
        // (assinatura "UnityFS"/"UnityRaw"/"UnityWeb" no início do arquivo,
        // como brody_tex_low.assets, que apesar do nome/extensão é na
        // verdade um bundle) e usa o caminho de carga correto para cada um.
        public async Task<bool> LoadAssetsFromBytesAsync(string fileName, byte[] fileBytes)
        {
            LastError = null;
            Rows.Clear();

            try
            {
                await EnsureClassPackageLoadedAsync();

                if (IsBundleFile(fileBytes))
                {
                    LoadFromBundle(fileName, fileBytes);
                }
                else
                {
                    var stream = new MemoryStream(fileBytes);

                    // "memPath" é só um nome lógico usado internamente pela
                    // lib para resolver dependências entre arquivos; não
                    // precisa existir de verdade no disco (não existe disco
                    // aqui).
                    CurrentFile = Manager.LoadAssetsFile(stream, fileName, true);
                }

                CurrentFileName = fileName;

                // Carrega os tipos (GameObject, Transform, etc.) para a
                // versão de engine específica deste arquivo.
                Manager.LoadClassDatabaseFromPackage(CurrentFile!.file.Metadata.UnityVersion);

                PopulateRows();
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Falha ao abrir '{fileName}': {ex.Message}";
                CurrentFile = null;
                return false;
            }
        }

        // AssetBundles da Unity começam com uma dessas assinaturas ASCII,
        // independentemente da extensão do arquivo (.bundle, .unity3d, ou
        // até .assets como no caso de brody_tex_low.assets).
        private static bool IsBundleFile(byte[] bytes)
        {
            if (bytes.Length < 8) return false;
            string sig = System.Text.Encoding.ASCII.GetString(bytes, 0, 7);
            return sig is "UnityFS" or "UnityWe" or "UnityRa";
        }

        // Carrega um AssetBundle e extrai o primeiro .assets serializado
        // de dentro dele. NOTA PARA QUEM FOR COMPILAR: não confirmei nesta
        // sessão (sem SDK instalado) o nome exato de todos os overloads
        // abaixo; são os métodos documentados na wiki da AssetsTools.NET
        // para esse fluxo (LoadBundleFile -> LoadAssetsFileFromBundle).
        // Bundles com múltiplos .assets internos: por ora só o primeiro é
        // carregado (bundles de textura única, como este caso, normalmente
        // só têm um).
        private void LoadFromBundle(string fileName, byte[] fileBytes)
        {
            var bundleStream = new MemoryStream(fileBytes);
            var bundleInst = Manager.LoadBundleFile(bundleStream, fileName);

            var assetsFileName = bundleInst.file.BlockAndDirInfo.DirectoryInfos
                .FirstOrDefault(d => !d.Name.EndsWith(".resS") && !d.Name.EndsWith(".resource"))
                ?.Name;

            if (assetsFileName == null)
                throw new InvalidOperationException("Nenhum arquivo .assets encontrado dentro do bundle.");

            CurrentFile = Manager.LoadAssetsFileFromBundle(bundleInst, assetsFileName, true);
        }

        private void PopulateRows()
        {
            if (CurrentFile == null) return;

            foreach (var info in CurrentFile.file.AssetInfos)
            {
                string typeName;
                try
                {
                    // TypeId corresponde ao enum AssetClassID (GameObject,
                    // Transform, MonoBehaviour, etc.) para tipos padrão da
                    // engine. Não existem "AssetHelper.FindAssetClassByID"
                    // nem "AssetsManager.classDatabase" na API pública desta
                    // versão da lib — o nome do tipo vem direto do enum.
                    typeName = ((AssetClassID)info.TypeId).ToString();
                }
                catch
                {
                    typeName = "Type_" + info.TypeId;
                }

                Rows.Add(new AssetRow
                {
                    PathId = info.PathId,
                    FileId = 0,
                    TypeId = info.TypeId,
                    TypeName = typeName,
                    ByteSize = info.ByteSize,
                    Container = "",
                    Name = ""
                });
            }
        }

        public void Close()
        {
            if (CurrentFile != null)
            {
                Manager.UnloadAll();
                CurrentFile = null;
                CurrentFileName = null;
                Rows.Clear();
            }
        }

        // Extrai e decodifica um asset do tipo Texture2D (TypeId == 28,
        // AssetClassID.Texture2D) para PNG, usando só formatos não
        // comprimidos (ver SimpleTextureDecoder). Retorna os bytes do
        // PNG prontos para exibir num <img> via data URI, ou null se
        // não for possível (formato comprimido, campo ausente, etc —
        // detalhe fica em TextureError).
        public byte[]? TryDecodeTexturePng(AssetRow row)
        {
            TextureError = null;

            if (CurrentFile == null)
            {
                TextureError = "Nenhum arquivo carregado.";
                return null;
            }

            try
            {
                var info = CurrentFile.file.AssetInfos.FirstOrDefault(i => i.PathId == row.PathId);
                if (info == null)
                {
                    TextureError = "Asset não encontrado.";
                    return null;
                }

                // GetBaseField é a mesma API usada no exemplo oficial da
                // AssetsTools.NET para ler campos de qualquer asset
                // (independe de classe auxiliar específica de Texture2D,
                // então funciona mesmo que a assinatura de TextureFile
                // varie entre versões do pacote).
                var baseField = Manager.GetBaseField(CurrentFile, info);
                if (baseField == null)
                {
                    TextureError = "Não foi possível ler os campos do asset (faltam templates de tipo para esta versão da engine).";
                    return null;
                }

                int width = baseField["m_Width"].AsInt;
                int height = baseField["m_Height"].AsInt;
                int formatId = baseField["m_TextureFormat"].AsInt;

                // NOTA PARA QUEM FOR COMPILAR: no UABEA original
                // (TextureHelper.GetByteArrayTexture), o campo "image data"
                // às vezes precisa ser forçado para AssetValueType.ByteArray
                // no TEMPLATE antes de materializar o valor, dependendo da
                // versão da engine. Se AsByteArray vier vazio/errado mesmo
                // com dados presentes, veja esse método no projeto original
                // como referência de ajuste.
                byte[] imageData = baseField["image data"].AsByteArray ?? Array.Empty<byte>();

                if (imageData == null || imageData.Length == 0)
                {
                    TextureError = "Os dados da imagem estão vazios neste asset — provavelmente a textura é 'streamed' (m_StreamData aponta pra um arquivo .resS externo dentro do mesmo bundle), que ainda não é lido automaticamente nesta versão.";
                    return null;
                }

                if (!SimpleTextureDecoder.IsSupported(formatId))
                {
                    TextureError = $"Formato de textura (id {formatId}) é comprimido (ETC/ASTC/PVRTC/DXT/BC) — este porte só decodifica formatos não-comprimidos por enquanto.";
                    return null;
                }

                byte[] rgba = SimpleTextureDecoder.DecodeToRgba32(imageData, width, height, formatId);

                // Unity guarda a textura com a primeira linha embaixo
                // (origem no canto inferior esquerdo); inverte para a
                // ordem padrão de imagem (topo primeiro) antes de gerar o PNG.
                byte[] flipped = FlipVertically(rgba, width, height);

                return PngEncoder.Encode(flipped, width, height);
            }
            catch (Exception ex)
            {
                TextureError = $"Erro ao decodificar textura: {ex.Message}";
                return null;
            }
        }

        private static byte[] FlipVertically(byte[] rgba, int width, int height)
        {
            int stride = width * 4;
            byte[] outBuf = new byte[rgba.Length];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(rgba, y * stride, outBuf, (height - 1 - y) * stride, stride);
            }
            return outBuf;
        }
    }
}
