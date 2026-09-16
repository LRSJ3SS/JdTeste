namespace UABEA.Web.Models
{
    // Representação simplificada de um asset para a tela de listagem.
    // Equivale, em espírito, ao que o UABEAvalonia mostra no DataGrid
    // principal (Forms/MainWindow.axaml), mas achatado para exibição
    // em uma tabela HTML simples.
    public class AssetRow
    {
        public long PathId { get; set; }
        public int FileId { get; set; }
        public string TypeName { get; set; } = "";
        public int TypeId { get; set; }
        public string Container { get; set; } = "";
        public long ByteSize { get; set; }
        public string Name { get; set; } = "";
    }
}
