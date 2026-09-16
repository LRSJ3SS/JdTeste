using AssetsTools.NET;
using AssetsTools.NET.Extra;
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

        // Chamado quando o usuário seleciona um .assets pelo <InputFile>.
        // fileBytes já deve estar carregado em memória (ver Pages/Home.razor).
        public async Task<bool> LoadAssetsFromBytesAsync(string fileName, byte[] fileBytes)
        {
            LastError = null;
            Rows.Clear();

            try
            {
                await EnsureClassPackageLoadedAsync();

                var stream = new MemoryStream(fileBytes);

                // "memPath" é só um nome lógico usado internamente pela lib
                // para resolver dependências entre arquivos; não precisa
                // existir de verdade no disco (não existe disco aqui).
                CurrentFile = Manager.LoadAssetsFile(stream, fileName, true);
                CurrentFileName = fileName;

                // Carrega os tipos (GameObject, Transform, etc.) para a
                // versão de engine específica deste arquivo.
                Manager.LoadClassDatabaseFromPackage(CurrentFile.file.Metadata.UnityVersion);

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

        private void PopulateRows()
{
    if (CurrentFile == null) return;

    foreach (var info in CurrentFile.file.AssetInfos)
    {
        string typeName = "desconhecido";
        try
        {
            typeName = AssetHelper.FindAssetClassByID(
                Manager.classDatabase, info.TypeId)?.Name ?? "Type_" + info.TypeId;
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
    }
}
