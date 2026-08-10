namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using ValueObjects;

/// <summary>
/// Valores legados exclusivamente para fixtures de domínio. Não é enum, não faz
/// parte do contrato HTTP e não é consultado por handlers: novos processos são
/// resolvidos pelo catálogo Configuração via <c>ITipoProcessoReader</c>.
/// </summary>
public static class TipoProcesso
{
    public static TipoProcessoSnapshot Nenhum { get; } = null!;
    public static TipoProcessoSnapshot SiSU => Criar("019fe8f8-1400-7000-8000-000000000001", "SiSU", "SiSU");
    public static TipoProcessoSnapshot PSIQ => Criar("019fe8f8-1400-7000-8000-000000000002", "PSIQ", "Processo Seletivo Indígena e Quilombola");
    public static TipoProcessoSnapshot PSECampo => Criar("019fe8f8-1400-7000-8000-000000000003", "PSECampo", "Processo Seletivo Especial do Campo");
    public static TipoProcessoSnapshot PSVR => Criar("019fe8f8-1400-7000-8000-000000000004", "PSVR", "Processo Seletivo Via Reserva");
    public static TipoProcessoSnapshot TransferenciaInterna => Criar("019fe8f8-1400-7000-8000-000000000005", "TransferenciaInterna", "Transferência Interna");
    public static TipoProcessoSnapshot TransferenciaExterna => Criar("019fe8f8-1400-7000-8000-000000000006", "TransferenciaExterna", "Transferência Externa");
    public static TipoProcessoSnapshot PortadorDiploma => Criar("019fe8f8-1400-7000-8000-000000000007", "PortadorDiploma", "Portador de Diploma");
    public static TipoProcessoSnapshot Reopcao => Criar("019fe8f8-1400-7000-8000-000000000008", "Reopcao", "Reopção");

    private static TipoProcessoSnapshot Criar(string id, string codigo, string nome) =>
        TipoProcessoSnapshot.Criar(Guid.Parse(id), codigo, nome).Value!;
}
