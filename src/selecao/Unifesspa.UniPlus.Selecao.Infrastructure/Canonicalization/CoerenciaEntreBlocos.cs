namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere a coerência entre os blocos lidos do envelope, pelas mesmas conferências que a escrita e a
/// publicação aplicam, e monta o <see cref="EnvelopeReidratado"/>. Um envelope adulterado que passe
/// pela leitura de cada bloco mas contradiga outro bloco é recusado como malformado, nunca reidratado
/// como se fosse íntegro.
/// </summary>
internal static class CoerenciaEntreBlocos
{
    /// <summary>
    /// Confere os blocos entre si e monta o envelope reidratado.
    /// </summary>
    /// <param name="blocos">Os blocos já lidos.</param>
    /// <param name="versao">A versão congelada de onde o envelope saiu.</param>
    /// <param name="payload">O objeto canônico, para comparar os blocos derivados com o congelado.</param>
    /// <returns>O envelope reidratado, ou a primeira incoerência.</returns>
    internal static Result<EnvelopeReidratado> Conferir(BlocosDoEnvelope blocos, VersaoConfiguracao versao, JsonObject payload)
    {
        IReadOnlyList<NoExigencia> todosOsNos = [.. blocos.Raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        if (PrimeiraIncoerencia(blocos, versao, payload, todosOsNos) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        IReadOnlyList<FatoColetado> itensEDeGrupos = [.. blocos.FatosColetados, .. blocos.GruposColetados.SelectMany(static g => g.Subitens)];
        Result<IReadOnlyList<OpcaoDeclaradaFato>> opcoesDeclaradas =
            LeituraDosValoresDeDominio.ReconstruirOpcoesDeclaradas(itensEDeGrupos, blocos.ValoresSelecionaveis);
        return opcoesDeclaradas.IsFailure
            ? Result<EnvelopeReidratado>.Failure(opcoesDeclaradas.Error!)
            : Result<EnvelopeReidratado>.Success(Montar(blocos, todosOsNos, opcoesDeclaradas.Value!));
    }

    private static DomainError? PrimeiraIncoerencia(
        BlocosDoEnvelope blocos, VersaoConfiguracao versao, JsonObject payload, IReadOnlyList<NoExigencia> todosOsNos)
    {
        return ItensForaDosFormularios(blocos.Formularios, blocos.FatosColetados, blocos.GruposColetados)
            ?? LeituraDaRetificacao.VerificarCoerenciaComAVersao(versao, blocos.HashDocumento, blocos.Retificacao)
            ?? VinculoOuRepeticaoInvalidos(blocos, todosOsNos)
            ?? LeituraDaDerivacao.ValidarBlocoDeFatosEDerivacao(
                new BlocoDeFatosEDerivacao(blocos.FatosColetados, blocos.GruposColetados, blocos.RegrasDerivacao, blocos.VersaoInterpretador),
                blocos.DocumentosExigidos,
                blocos.Formularios,
                blocos.TermosExigidos,
                blocos.Distribuicao,
                payload)
            ?? CitacoesOuEnemInvalidos(blocos);
    }

    // Onde cada documento é coletado só significa alguma coisa contra o cronograma e as etapas repostas ao
    // lado dele, e a repetição por entidade nomeia um grupo dos formulários repostos, pela mesma conferência
    // da escrita. Sem elas, uma exigência apontando para fase ou etapa que o envelope não repõe atravessa a
    // leitura e morre na chave estrangeira.
    private static DomainError? VinculoOuRepeticaoInvalidos(BlocosDoEnvelope blocos, IReadOnlyList<NoExigencia> todosOsNos) =>
        ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(blocos.DocumentosExigidos, blocos.CronogramaFases, blocos.Etapas)
        ?? ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(todosOsNos, blocos.GruposColetados);

    // As regras dos itens, das seções e dos termos citam só o que o formulário delas conhece, pela mesma
    // conferência da escrita e da publicação, que inclui os fatos que a oferta deriva.
    private static DomainError? CitacoesOuEnemInvalidos(BlocosDoEnvelope blocos)
    {
        if (Domain.ProcessosSeletivos.Formularios.CitacoesDosFormularios.CitacaoInvalidaNosFormularios(
                blocos.Formularios,
                blocos.FatosColetados,
                blocos.TermosExigidos,
                DerivacoesDaOferta.Efetivas(blocos.RegrasDerivacao, blocos.Distribuicao),
                blocos.GruposColetados) is { } citacaoInvalida)
        {
            return new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'fatosColetados': {citacaoInvalida.Message}");
        }

        return Domain.ProcessosSeletivos.Formularios.PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(
                [.. blocos.FatosColetados, .. blocos.GruposColetados.SelectMany(static g => g.Subitens)], blocos.Classificacao) is { } edicaoSemEnem
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'fatosColetados': {edicaoSemEnem.Message}")
            : null;
    }

    private static EnvelopeReidratado Montar(
        BlocosDoEnvelope blocos, IReadOnlyList<NoExigencia> todosOsNos, IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas)
    {
        GrafoConfiguracao grafo = new(
            blocos.Etapas, blocos.Atendimento!, blocos.Distribuicao, blocos.Bonus, blocos.Desempate, blocos.Classificacao!, blocos.CronogramaFases,
            blocos.DocumentosExigidos, todosOsNos, blocos.ReferenciaTemporalFatos, blocos.FatosColetados, blocos.RegrasDerivacao,
            cascataRemanejamento: blocos.Cascata,
            formularios: blocos.Formularios,
            termosExigidos: blocos.TermosExigidos,
            configuracaoDivulgacao: blocos.Divulgacao,
            configuracaoTaxaInscricao: blocos.TaxaInscricao,
            localidade: blocos.Localidade,
            numeroDeOpcoesDeCurso: blocos.NumeroDeOpcoesDeCurso,
            algoritmoContagemPrazo: blocos.AlgoritmoContagemPrazo,
            identificadorLegivel: blocos.IdentificadorLegivel,
            opcoesDeclaradas: opcoesDeclaradas,
            gruposColetados: blocos.GruposColetados);
        return new EnvelopeReidratado(
            grafo, blocos.Dados!, blocos.HashDocumento, blocos.FusoHorario!, blocos.Retificacao, blocos.Conformidade,
            blocos.MetadadosFatosCongelados, blocos.ValoresSelecionaveis, blocos.CalendarioDiasUteis, blocos.AgregadosDosGrupos,
            blocos.ApresentacaoDosPressupostos, blocos.TiposDeAtoCongelados, blocos.FatosDerivadosCongelados);
    }

    /// <summary>
    /// Todo item pertence a um formulário do envelope e está numa seção dele, na ordem das seções: a
    /// mesma recusa da publicação, que nunca congela outra forma.
    /// </summary>
    private static DomainError? ItensForaDosFormularios(
        IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
    {
        Dictionary<FinalidadeFormulario, FormularioProcesso> porFinalidade = formularios.ToDictionary(static f => f.Finalidade);
        IEnumerable<(FinalidadeFormulario Finalidade, ItemEstrutura Item)> itensEGrupos = fatos
            .Select(static f => (f.Finalidade, new ItemEstrutura(f.FatoCodigo, f.Ordem, f.EtapaCodigo)))
            .Concat(grupos.Select(static g => (g.Finalidade, g.ParaEstrutura())));
        foreach (IGrouping<FinalidadeFormulario, ItemEstrutura> itens in itensEGrupos.GroupBy(static p => p.Finalidade, static p => p.Item))
        {
            if (!porFinalidade.TryGetValue(itens.Key, out FormularioProcesso? formulario))
            {
                return new DomainError(ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'fatosColetados' tem itens de {EstruturaFormulario.ParaToken(itens.Key)}, que não tem formulário no envelope.");
            }

            if (EstruturaFormulario.ValidarItens(formulario.Estrutura, [.. itens]) is [{ } primeiro, ..])
            {
                return new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'fatosColetados': {primeiro.Error.Message}");
            }
        }

        return null;
    }
}
