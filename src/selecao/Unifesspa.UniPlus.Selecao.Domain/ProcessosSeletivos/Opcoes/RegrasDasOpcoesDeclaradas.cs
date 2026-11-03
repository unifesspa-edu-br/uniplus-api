namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDasOpcoesDeclaradas
{
    internal static DomainError? ConferirSubstituicao(
        EstadoDaConfiguracao estado, string fato, IReadOnlyList<OpcaoDeclaradaFato> opcoes, IReadOnlyCollection<string>? agregadosDoFato)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (OfertaAtendimentoEspecializado.GereOpcoesDoFato(fato))
        {
            return new DomainError(
                OpcaoDeclaradaFatoErrorCodes.GeridasPelaOfertaDeAtendimento,
                "As opções deste fato vêm da oferta de atendimento especializado; defina-as na oferta.");
        }

        // A edição do ENEM só tem sentido quando a classificação lê notas do ENEM do candidato.
        if (string.Equals(fato, EdicaoDoEnem.Fato, StringComparison.Ordinal) && estado.Classificacao is not { BaseadoEmEnem: true })
        {
            return new DomainError(
                OpcaoDeclaradaFatoErrorCodes.EdicaoEnemForaDeProcessoEnem,
                "As edições do ENEM só podem ser declaradas quando a classificação do processo é baseada em ENEM.");
        }

        if (opcoes.Count == 0)
        {
            return new DomainError(OpcaoDeclaradaFatoErrorCodes.ListaVazia, "Declare ao menos uma opção para o fato.");
        }

        HashSet<string> codigos = new(StringComparer.Ordinal);
        if (opcoes.Any(o => !codigos.Add(o.Codigo)))
        {
            return new DomainError(OpcaoDeclaradaFatoErrorCodes.CodigoRepetido, "Cada opção do fato precisa de um código próprio.");
        }

        return DeixariaCondicaoVivaSemOpcao(estado, fato, codigos, agregadosDoFato)
            ? new DomainError(
                OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva,
                "Uma condição viva (exigência documental, pré-condição de campo ou regra de derivação) cita uma opção que deixaria de existir — ajuste ou remova a condição antes de redefinir as opções.")
            : null;
    }

    private static bool DeixariaCondicaoVivaSemOpcao(
        EstadoDaConfiguracao estado, string fato, HashSet<string> codigos, IReadOnlyCollection<string>? agregadosDoFato) =>
        ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, fato, codigos)
        || (agregadosDoFato ?? []).Any(agregado => ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, agregado, codigos));

    /// <summary>A origem das opções de um campo coletado: a copiada do catálogo, salvo condição e recurso de atendimento, que são sempre do processo porque a oferta é a dona deles.</summary>
    public static OrigemValoresColeta OrigemDasOpcoes(FatoColetado fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return OfertaAtendimentoEspecializado.ImpoeOrigemDoFato(fato.FatoCodigo)
            ? OrigemValoresColeta.OpcoesDoProcesso
            : fato.OrigemValores;
    }

    public static IReadOnlyList<OpcaoDoProcesso> OpcoesDoProcesso(EstadoDaConfiguracao estado, string fatoCodigo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);

        if (OfertaAtendimentoEspecializado.GereOpcoesDoFato(fatoCodigo))
        {
            return estado.OfertaAtendimento?.OpcoesDoFato(fatoCodigo) ?? [];
        }

        return [.. estado.OpcoesDeclaradas
            .Where(o => string.Equals(o.FatoCodigo, fatoCodigo, StringComparison.Ordinal))
            .OrderBy(static o => o.Ordem)
            .ThenBy(static o => o.Codigo, StringComparer.Ordinal)
            .Select(static o => new OpcaoDoProcesso(o.Codigo, o.Rotulo, o.Ordem))];
    }
}
