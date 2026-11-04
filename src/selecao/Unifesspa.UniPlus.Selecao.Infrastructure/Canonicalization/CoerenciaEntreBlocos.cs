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

        IReadOnlyList<FatoColetado> itensEDeGrupos = [.. blocos.Coleta.FatosColetados, .. blocos.Coleta.GruposColetados.SelectMany(static g => g.Subitens)];
        Result<IReadOnlyList<OpcaoDeclaradaFato>> opcoesDeclaradas =
            LeituraDosValoresDeDominio.ReconstruirOpcoesDeclaradas(itensEDeGrupos, blocos.Coleta.ValoresSelecionaveis);
        return opcoesDeclaradas.IsFailure
            ? Result<EnvelopeReidratado>.Failure(opcoesDeclaradas.Error!)
            : Result<EnvelopeReidratado>.Success(Montar(blocos, todosOsNos, opcoesDeclaradas.Value!));
    }

    private static DomainError? PrimeiraIncoerencia(
        BlocosDoEnvelope blocos, VersaoConfiguracao versao, JsonObject payload, IReadOnlyList<NoExigencia> todosOsNos)
    {
        return ItensForaDosFormularios(blocos.Documentos.Formularios, blocos.Coleta.FatosColetados, blocos.Coleta.GruposColetados)
            ?? LeituraDaRetificacao.VerificarCoerenciaComAVersao(versao, blocos.Configuracao.HashDocumento, blocos.Retificacao)
            ?? VinculoOuRepeticaoInvalidos(blocos, todosOsNos)
            ?? LeituraDaDerivacao.ValidarBlocoDeFatosEDerivacao(
                new BlocoDeFatosEDerivacao(blocos.Coleta.FatosColetados, blocos.Coleta.GruposColetados, blocos.Coleta.RegrasDerivacao, blocos.Coleta.VersaoInterpretador),
                blocos.Documentos.DocumentosExigidos,
                blocos.Documentos.Formularios,
                blocos.Documentos.TermosExigidos,
                blocos.Configuracao.Distribuicao,
                payload)
            ?? CitacoesOuEnemInvalidos(blocos);
    }

    // Onde cada documento é coletado só significa alguma coisa contra o cronograma e as etapas repostas ao
    // lado dele, e a repetição por entidade nomeia um grupo dos formulários repostos, pela mesma conferência
    // da escrita. Sem elas, uma exigência apontando para fase ou etapa que o envelope não repõe atravessa a
    // leitura e morre na chave estrangeira.
    private static DomainError? VinculoOuRepeticaoInvalidos(BlocosDoEnvelope blocos, IReadOnlyList<NoExigencia> todosOsNos) =>
        ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(blocos.Documentos.DocumentosExigidos, blocos.Configuracao.CronogramaFases, blocos.Configuracao.Etapas)
        ?? ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(todosOsNos, blocos.Coleta.GruposColetados);

    // As regras dos itens, das seções e dos termos citam só o que o formulário delas conhece, pela mesma
    // conferência da escrita e da publicação, que inclui os fatos que a oferta deriva.
    private static DomainError? CitacoesOuEnemInvalidos(BlocosDoEnvelope blocos)
    {
        if (Domain.ProcessosSeletivos.Formularios.CitacoesDosFormularios.CitacaoInvalidaNosFormularios(
                blocos.Documentos.Formularios,
                blocos.Coleta.FatosColetados,
                blocos.Documentos.TermosExigidos,
                DerivacoesDaOferta.Efetivas(blocos.Coleta.RegrasDerivacao, blocos.Configuracao.Distribuicao),
                blocos.Coleta.GruposColetados) is { } citacaoInvalida)
        {
            return new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'fatosColetados': {citacaoInvalida.Message}");
        }

        return Domain.ProcessosSeletivos.Formularios.PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(
                [.. blocos.Coleta.FatosColetados, .. blocos.Coleta.GruposColetados.SelectMany(static g => g.Subitens)], blocos.Configuracao.Classificacao) is { } edicaoSemEnem
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'fatosColetados': {edicaoSemEnem.Message}")
            : null;
    }

    private static EnvelopeReidratado Montar(
        BlocosDoEnvelope blocos, IReadOnlyList<NoExigencia> todosOsNos, IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas)
    {
        GrafoConfiguracao grafo = new(
            blocos.Configuracao.Etapas, blocos.Configuracao.Atendimento!, blocos.Configuracao.Distribuicao, blocos.Configuracao.Bonus, blocos.Configuracao.Desempate, blocos.Configuracao.Classificacao!, blocos.Configuracao.CronogramaFases,
            blocos.Documentos.DocumentosExigidos, todosOsNos, blocos.Documentos.ReferenciaTemporalFatos, blocos.Coleta.FatosColetados, blocos.Coleta.RegrasDerivacao,
            cascataRemanejamento: blocos.Configuracao.Cascata,
            formularios: blocos.Documentos.Formularios,
            termosExigidos: blocos.Documentos.TermosExigidos,
            configuracaoDivulgacao: blocos.Publicacao.Divulgacao,
            configuracaoTaxaInscricao: blocos.Publicacao.TaxaInscricao,
            localidade: blocos.Publicacao.Localidade,
            numeroDeOpcoesDeCurso: blocos.Publicacao.NumeroDeOpcoesDeCurso,
            algoritmoContagemPrazo: blocos.Publicacao.AlgoritmoContagemPrazo,
            identificadorLegivel: blocos.Publicacao.IdentificadorLegivel,
            opcoesDeclaradas: opcoesDeclaradas,
            gruposColetados: blocos.Coleta.GruposColetados);
        return new EnvelopeReidratado(
            grafo, blocos.Configuracao.Dados!, blocos.Configuracao.HashDocumento, blocos.Publicacao.FusoHorario!, blocos.Retificacao, blocos.Documentos.Conformidade,
            blocos.Documentos.MetadadosFatosCongelados, blocos.Coleta.ValoresSelecionaveis, blocos.Publicacao.CalendarioDiasUteis, blocos.Coleta.AgregadosDosGrupos,
            blocos.Coleta.ApresentacaoDosPressupostos, blocos.Coleta.TiposDeAtoCongelados, blocos.Coleta.FatosDerivadosCongelados);
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
