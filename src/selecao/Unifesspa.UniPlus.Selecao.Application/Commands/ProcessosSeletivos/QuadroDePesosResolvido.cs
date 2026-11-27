namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal sealed record QuadroDePesosResolvido(
    string? Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Quadro, IReadOnlyList<FieldError> Recusas)
{
    public static QuadroDePesosResolvido SemConsulta(string? informada) => new(informada, [], []);

    public static QuadroDePesosResolvido Recusado(string? informada, IReadOnlyList<FieldError> recusas) => new(informada, [], recusas);

    /// <summary>A forma é conferida antes da busca, pois o banco recusa caracteres nulos.</summary>
    public static async Task<QuadroDePesosResolvido> DaResolucaoAsync(
        string resolucao, IPesoAreaEnemReader leitor, CancellationToken cancellationToken)
    {
        List<FieldError> recusas = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(resolucao);
        if (recusas.Count > 0)
        {
            return Recusado(resolucao, recusas);
        }

        ResolucaoPesoAreaEnemView? cadastrada = await leitor.ObterPorResolucaoAsync(resolucao, cancellationToken).ConfigureAwait(false);
        return DaResolucaoCadastrada(resolucao, cadastrada);
    }

    public static QuadroDePesosResolvido DaResolucaoCadastrada(string informada, ResolucaoPesoAreaEnemView? cadastrada) => cadastrada switch
    {
        null => Recusado(informada, [NaResolucao("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada",
            "A resolução de Pesos por Área informada não tem pesos cadastrados.")]),
        { Completa: false } => Recusado(informada, [NaResolucao("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta",
            $"A resolução de Pesos por Área {cadastrada.Resolucao} não tem pesos cadastrados para: "
            + $"{string.Join(", ", cadastrada.GruposAusentes.Select(static grupo => grupo.Rotulo))}.")]),
        _ => CopiarLinhas(informada, cadastrada),
    };

    public DeclaracaoDaClassificacao DeclaracaoCompleta(DeclaracaoDaClassificacao regras) =>
        regras with { ResolucaoPesoAreaEnem = Resolucao, QuadroPesoAreaEnem = Quadro };

    /// <summary>Acumula as recusas do domínio, sem as que são consequência da resolução recusada.</summary>
    public List<FieldError> RecusasJuntoCom(Result<ConfiguracaoClassificacao> configuracao)
    {
        List<FieldError> erros = [.. Recusas];
        if (configuracao.IsFailure)
        {
            erros.AddRange(Recusas.Count == 0 ? configuracao.Errors
                : ConfiguracaoClassificacao.SemConsequenciasDaResolucaoRecusada(configuracao.Errors));
        }

        return erros;
    }

    private static QuadroDePesosResolvido CopiarLinhas(string informada, ResolucaoPesoAreaEnemView cadastrada)
    {
        List<GrupoPesoAreaEnemCongelado> quadro = [];
        List<FieldError> erros = [];
        foreach (PesoAreaEnemView linha in cadastrada.Linhas)
        {
            Result<GrupoPesoAreaEnemCongelado> grupo = CopiarLinha(linha);
            if (grupo.IsFailure)
            {
                erros.AddRange(grupo.Errors.Select(static erro => new FieldError(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, erro.Error)));
                continue;
            }
            quadro.Add(grupo.Value!);
        }

        return erros.Count > 0 ? Recusado(informada, erros) : new(cadastrada.Resolucao, quadro, []);
    }

    private static Result<GrupoPesoAreaEnemCongelado> CopiarLinha(PesoAreaEnemView linha) => GrupoPesoAreaEnemCongelado.Criar(
        linha.GrupoCurso.Codigo, linha.GrupoCurso.Rotulo, linha.BaseLegal,
        linha.Areas.Select(static area => ((string?)area.Codigo, (string?)area.Rotulo, area.Peso, area.Corte)));

    private static FieldError NaResolucao(string codigo, string mensagem) =>
        new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(codigo, mensagem));
}
