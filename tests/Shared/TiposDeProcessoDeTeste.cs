namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Tipos de processo fixos para fixtures de teste. O conjunto real é configuração: vem do catálogo do módulo
/// Configuração, resolvido por <c>ITipoProcessoReader</c>, e nenhum código de produção enumera tipos.
/// </summary>
public static class TiposDeProcessoDeTeste
{
    public static TipoProcessoSnapshot Nenhum { get; } = null!;
    public static TipoProcessoSnapshot Sisu => Criar("019fe8f8-1400-7000-8000-000000000001", "SISU", "SiSU");
    public static TipoProcessoSnapshot IndigenaEQuilombola => Criar("019fe8f8-1400-7000-8000-000000000002", "INDIGENA_E_QUILOMBOLA", "Processo Seletivo Indígena e Quilombola");
    public static TipoProcessoSnapshot EducacaoDoCampo => Criar("019fe8f8-1400-7000-8000-000000000003", "EDUCACAO_DO_CAMPO", "Processo Seletivo Especial do Campo");
    public static TipoProcessoSnapshot VagasRemanescentes => Criar("019fe8f8-1400-7000-8000-000000000004", "VAGAS_REMANESCENTES", "Processo Seletivo de Vagas Remanescentes");
    public static TipoProcessoSnapshot TransferenciaInterna => Criar("019fe8f8-1400-7000-8000-000000000005", "TRANSFERENCIA_INTERNA", "Transferência Interna");
    public static TipoProcessoSnapshot TransferenciaExterna => Criar("019fe8f8-1400-7000-8000-000000000006", "TRANSFERENCIA_EXTERNA", "Transferência Externa");
    public static TipoProcessoSnapshot PortadorDeDiploma => Criar("019fe8f8-1400-7000-8000-000000000007", "PORTADOR_DE_DIPLOMA", "Portador de Diploma");
    public static TipoProcessoSnapshot Reopcao => Criar("019fe8f8-1400-7000-8000-000000000008", "REOPCAO", "Reopção");

    private static TipoProcessoSnapshot Criar(string id, string codigo, string nome) =>
        TipoProcessoSnapshot.Criar(Guid.Parse(id), codigo, nome).Value!;
}
