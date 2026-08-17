namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração de taxa de inscrição e fundamentos de isenção do processo (issue #1112).
/// Ausência desta entidade em <see cref="ProcessoSeletivo.ConfiguracaoTaxaInscricao"/> significa
/// "ainda não declarado" — recusa a publicação (CA-01). Não confundir com <see cref="Cobra"/> ==
/// <see langword="false"/>, que é a declaração explícita de que o processo não cobra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Três estados no comando que grava esta configuração, dois na entidade.</b> No wire,
/// <c>Cobra</c> nulo remove a declaração (o processo volta a "não declarado" — reverte inclusive
/// uma declaração anterior); <c>Cobra</c> não nulo cria ou substitui integralmente. Uma vez
/// instanciada, a entidade sempre representa uma declaração definitiva — por isso <see cref="Cobra"/>
/// aqui é <see langword="bool"/> não anulável.
/// </para>
/// <para>
/// <b>"Não cobrar" e "isentar" são mutuamente exclusivos</b> (CA-03): <see cref="Cobra"/> ==
/// <see langword="false"/> recusa qualquer fundamento configurado. <see cref="Fundamentos"/> é
/// guardado deduplicado e na ordem canônica (código ordinal), mesmo raciocínio de
/// <see cref="ConfiguracaoDivulgacao.CamposPublicos"/>.
/// </para>
/// <para>
/// <b>CA-06 — confirmação explícita, não ramo por tipo de processo.</b> O agregado não tem
/// discriminador confiável de "processo de Medicina" (tipo de processo é cadastro configurável;
/// Medicina aparece como curso/oferta), e a suíte de arquitetura proíbe branch por tipo/curso
/// concreto. Por isso, referenciar qualquer fundamento — em qualquer processo — exige
/// <see cref="ConfirmacaoFundamentos"/> == <see langword="true"/>: o sistema não decide o que é
/// Medicina, só registra que o administrador confirmou explicitamente a referência.
/// </para>
/// </remarks>
public sealed class ConfiguracaoTaxaInscricao : EntityBase
{
    /// <summary>Precisão/escala da coluna de valor monetário — espelhada em <c>LimitesDoEnvelope.PrecisaoTaxaInscricao</c>.</summary>
    public const int ValorPrecisao = 12;

    public const int ValorEscala = 2;

    public Guid ProcessoSeletivoId { get; private set; }

    public bool Cobra { get; private set; }

    /// <summary>Positivo quando <see cref="Cobra"/>; sempre nulo quando não cobra (CA-02/CA-03).</summary>
    public decimal? Valor { get; private set; }

    /// <summary>Fundamentos de isenção referenciados, deduplicados e na ordem canônica. Vazio é estado válido (CA-04).</summary>
    public IReadOnlyList<FundamentoIsencao> Fundamentos { get; private set; } = [];

    /// <summary>Confirmação explícita do administrador ao referenciar fundamentos (CA-06) — irrelevante quando <see cref="Fundamentos"/> é vazio.</summary>
    public bool ConfirmacaoFundamentos { get; private set; }

    private ConfiguracaoTaxaInscricao() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote: cobrança/valor, exclusividade com isenção, vocabulário dos fundamentos e
    /// confirmação podem coexistir no mesmo payload malformado.
    /// </summary>
    public static Result<ConfiguracaoTaxaInscricao> Criar(
        bool cobra,
        decimal? valor,
        IReadOnlyList<string>? fundamentosCodigos,
        bool confirmacaoFundamentos)
    {
        List<FieldError> erros = [];

        if (cobra)
        {
            if (valor is not { } valorInformado || valorInformado <= 0)
            {
                erros.Add(new("valor", new DomainError(
                    "ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra",
                    "Processo que cobra taxa exige valor positivo.")));
            }
        }
        else if (valor is not null)
        {
            erros.Add(new("valor", new DomainError(
                "ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra",
                "Processo que declara não cobrar taxa não pode informar valor.")));
        }

        List<FundamentoIsencao> fundamentos = [];
        if (fundamentosCodigos is { Count: > 0 })
        {
            if (!cobra)
            {
                erros.Add(new("fundamentos", new DomainError(
                    "ConfiguracaoTaxaInscricao.FundamentoExigeCobranca",
                    "Processo que declara não cobrar taxa não pode configurar fundamento de isenção — "
                    + "\"não cobrar\" e \"isentar\" são decisões distintas e mutuamente exclusivas.")));
            }

            // Vocabulário e confirmação rodam independente de `cobra`: cada um é uma
            // violação detectável por si só (o token não pertence ao catálogo, ou falta
            // confirmação), então reportá-los junto de FundamentoExigeCobranca no mesmo
            // lote poupa o cliente de descobri-los um de cada vez, a cada reenvio.
            bool temFundamentoDesconhecido = false;
            foreach (string codigo in fundamentosCodigos)
            {
                FundamentoIsencao fundamento = FundamentoIsencaoCodigo.FromCodigo(codigo);
                if (fundamento == FundamentoIsencao.Nenhum)
                {
                    temFundamentoDesconhecido = true;
                    continue;
                }

                fundamentos.Add(fundamento);
            }

            fundamentos = [.. fundamentos.Distinct().OrderBy(static f => f.ToCodigo(), StringComparer.Ordinal)];

            if (temFundamentoDesconhecido)
            {
                // ADR-0023: errors[].message nunca ecoa o valor rejeitado — sem o código
                // específico, mesmo raciocínio do vocabulário de ConfiguracaoDivulgacao.
                erros.Add(new("fundamentos", new DomainError(
                    "ConfiguracaoTaxaInscricao.FundamentoDesconhecido",
                    "Um ou mais fundamentos de isenção informados não pertencem ao catálogo conhecido.")));
            }

            if (!confirmacaoFundamentos)
            {
                erros.Add(new("confirmacaoFundamentos", new DomainError(
                    "ConfiguracaoTaxaInscricao.ConfirmacaoFundamentosObrigatoria",
                    "Referenciar fundamento de isenção exige confirmação explícita do administrador.")));
            }
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoTaxaInscricao>.ValidationFailure(erros);
        }

        return Result<ConfiguracaoTaxaInscricao>.Success(new ConfiguracaoTaxaInscricao
        {
            Cobra = cobra,
            Valor = valor,
            Fundamentos = fundamentos,
            ConfirmacaoFundamentos = fundamentos.Count > 0 && confirmacaoFundamentos,
        });
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
