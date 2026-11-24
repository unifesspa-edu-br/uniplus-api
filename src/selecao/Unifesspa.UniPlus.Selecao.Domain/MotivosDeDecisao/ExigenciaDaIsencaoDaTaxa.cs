namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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

    public IEnumerable<FieldError> TiposNaoDeclarados(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);

        return lugares.QueDeclaram(TipoMotivoDeDecisao.IsencaoDaTaxa).Any() || lugares.DeclaracaoDosTiposComParteRecusada
            ? []
            : FundamentosHabilitados(configuracao).Select(SemLugarQueDeclareOTipo);
    }

    public IEnumerable<FieldError> FaltasDeCobertura(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDeDecisaoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);
        ArgumentNullException.ThrowIfNull(escolhidos);

        IReadOnlyList<FundamentoIsencao> fundamentos = FundamentosHabilitados(configuracao);

        return from lugar in lugares.QueDeclaram(TipoMotivoDeDecisao.IsencaoDaTaxa)
               from fundamento in fundamentos
               from sentido in Sentidos
               where !escolhidos.Any(e => Cobre(e, lugar, fundamento.Codigo(), sentido))
               select Falta(lugar, fundamento.Codigo(), sentido);
    }

    private static IReadOnlyList<FundamentoIsencao> FundamentosHabilitados(ConfiguracaoDeclarada configuracao) =>
        configuracao.DivulgacaoETaxa.ConfiguracaoTaxaInscricao is { Cobra: true } taxa ? taxa.Fundamentos : [];

    private static bool Cobre(MotivoDeDecisaoDoProcesso escolhido, LugarNoProcesso lugar, string fundamento, SentidoDoMotivo sentido) =>
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
            $"O fundamento de isenção {fundamento.Codigo()} é habilitado, mas nenhuma fase nem etapa declara o tipo de decisão {TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo()}."));
}
