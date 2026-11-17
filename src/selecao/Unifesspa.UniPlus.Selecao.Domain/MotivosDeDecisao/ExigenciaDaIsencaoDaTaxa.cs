namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura dos motivos da decisão sobre a isenção da taxa (UNI-REQ-0123): em cada fase ou etapa que declara o tipo
/// <c>ISENCAO_DA_TAXA</c>, ao menos um motivo favorável e um desfavorável escolhidos ali para cada fundamento de isenção
/// habilitado. O fundamento sem fase nem etapa que declare o tipo não tem onde ser decidido, salvo quando a leitura recusou
/// parte do que declara os tipos: então não se sabe se algum lugar o declara, e a falta não é apontada.
/// </summary>
public sealed class ExigenciaDaIsencaoDaTaxa : IExigenciaDeMotivos
{
    private static readonly SentidoDoMotivo[] Sentidos = [SentidoDoMotivo.Favoravel, SentidoDoMotivo.Desfavoravel];

    public IEnumerable<FieldError> Faltas(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);
        ArgumentNullException.ThrowIfNull(escolhidos);

        IReadOnlyList<FundamentoIsencao> fundamentos = configuracao.DivulgacaoETaxa.ConfiguracaoTaxaInscricao is { Cobra: true } taxa ? taxa.Fundamentos : [];
        LugarNoProcesso[] declarantes = [.. lugares.QueDeclaram(TipoMotivoDeDecisao.IsencaoDaTaxa)];

        return declarantes.Length == 0
            ? FundamentosSemLugar(fundamentos, lugares)
            : from lugar in declarantes
              from fundamento in fundamentos
              from sentido in Sentidos
              where !escolhidos.Any(e => Cobre(e, lugar, fundamento.ToCodigo(), sentido))
              select Falta(lugar, fundamento.ToCodigo(), sentido);
    }

    private static IEnumerable<FieldError> FundamentosSemLugar(IReadOnlyList<FundamentoIsencao> fundamentos, LugaresDaDecisao lugares) =>
        lugares.DeclaracaoDosTiposComParteRecusada ? [] : fundamentos.Select(SemLugarQueDeclareOTipo);

    private static bool Cobre(MotivoDoProcesso escolhido, LugarNoProcesso lugar, string fundamento, SentidoDoMotivo sentido) =>
        escolhido.Lugar == lugar.Lugar
        && escolhido.Tipo == TipoMotivoDeDecisao.IsencaoDaTaxa
        && escolhido.Sentido == sentido
        && string.Equals(escolhido.Subclassificacao, fundamento, StringComparison.Ordinal);

    private static FieldError Falta(LugarNoProcesso lugar, string fundamento, SentidoDoMotivo sentido) =>
        new(MotivosEscolhidos.CampoDosMotivos, new DomainError(
            MotivoDeDecisaoErrorCodes.CoberturaInsuficiente,
            $"{lugar.Descricao} precisa de ao menos um motivo {sentido.Rotulo()} para o fundamento de isenção {fundamento}."));

    private static FieldError SemLugarQueDeclareOTipo(FundamentoIsencao fundamento) =>
        new("taxaDeInscricao.fundamentos", new DomainError(
            MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
            $"O fundamento de isenção {fundamento.ToCodigo()} é habilitado, mas nenhuma fase nem etapa declara o tipo de decisão {TipoMotivoDeDecisao.IsencaoDaTaxa.ToCodigo()}."));
}
