namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
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
        IReadOnlyList<RegraDeComprovacao> todasAsRegras = [.. blocos.RegrasSemGrupo.SelectMany(static regra => regra.EnumerarRegraEItens())];
        if (PrimeiraIncoerencia(blocos, versao, payload, todasAsRegras) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        IReadOnlyList<FatoColetado> itensEDeGrupos = [.. blocos.Coleta.FatosColetados, .. blocos.Coleta.GruposColetados.SelectMany(static g => g.Subitens)];
        Result<IReadOnlyList<OpcaoDeclaradaFato>> opcoesDeclaradas =
            LeituraDosValoresDeDominio.ReconstruirOpcoesDeclaradas(itensEDeGrupos, blocos.Coleta.ValoresSelecionaveis);
        return opcoesDeclaradas.IsFailure
            ? Result<EnvelopeReidratado>.Failure(opcoesDeclaradas.Error!)
            : Result<EnvelopeReidratado>.Success(Montar(blocos, todasAsRegras, itensEDeGrupos, opcoesDeclaradas.Value!));
    }

    private static DomainError? PrimeiraIncoerencia(
        BlocosDoEnvelope blocos, VersaoConfiguracao versao, JsonObject payload, IReadOnlyList<RegraDeComprovacao> todasAsRegras)
    {
        return ItensForaDosFormularios(blocos.Documentos.Formularios, blocos.Coleta.FatosColetados, blocos.Coleta.GruposColetados)
            ?? LeituraDaRetificacao.VerificarCoerenciaComAVersao(versao, blocos.Configuracao.HashDocumento, blocos.Retificacao)
            ?? VinculoOuRepeticaoInvalidos(blocos, todasAsRegras)
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
    private static DomainError? VinculoOuRepeticaoInvalidos(BlocosDoEnvelope blocos, IReadOnlyList<RegraDeComprovacao> todasAsRegras) =>
        ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(blocos.Documentos.DocumentosExigidos, blocos.Configuracao.CronogramaFases, blocos.Configuracao.Etapas)
        ?? ExigenciaEmFormularioInvalido(blocos.Documentos.DocumentosExigidos, blocos.Documentos.Formularios)
        ?? ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(todasAsRegras, blocos.Coleta.GruposColetados);

    // A exigência cita um formulário que o envelope repõe e que é o da fase dela, pela mesma conferência da
    // escrita e da publicação. O código lido só tem o formato conferido; sem esta checagem, um envelope de
    // hash correto reidrataria exigência em formulário inexistente ou de outra fase.
    internal static DomainError? ExigenciaEmFormularioInvalido(IReadOnlyList<DocumentoExigido> exigencias, IReadOnlyList<FormularioProcesso> formularios) =>
        ConferenciaDasExigenciasDoFormulario.Conferir(
            formularios.Select(static formulario => formulario.Declarado), [.. exigencias.Select(ExigenciaDeDocumentoDoFormulario.De)]).Count > 0
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, "'documentosExigidos.exigencias': uma exigência cita formulário que não está no envelope ou que é de outra fase.")
            : null;

    // As regras dos itens, das seções e dos termos citam só o que o formulário delas conhece, pela mesma
    // conferência da escrita e da publicação, que inclui os fatos que a oferta deriva.
    private static DomainError? CitacoesOuEnemInvalidos(BlocosDoEnvelope blocos) =>
        CitacaoInvalida(blocos) ?? EdicaoDoEnemInvalida(blocos);

    private static DomainError? CitacaoInvalida(BlocosDoEnvelope blocos) =>
        Domain.ProcessosSeletivos.Formularios.CitacoesDosFormularios.CitacaoInvalidaNosFormularios(
            blocos.Configuracao.CronogramaFases,
            blocos.Documentos.Formularios,
            blocos.Coleta.FatosColetados,
            blocos.Documentos.TermosExigidos,
            DerivacoesDaOferta.Efetivas(blocos.Coleta.RegrasDerivacao, blocos.Configuracao.Distribuicao),
            blocos.Coleta.GruposColetados) is not null
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, "'dadosDosFormularios': uma citação não corresponde aos dados conhecidos pelo formulário.")
            : null;

    private static DomainError? EdicaoDoEnemInvalida(BlocosDoEnvelope blocos) =>
        Domain.ProcessosSeletivos.Formularios.PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(
            [.. blocos.Coleta.FatosColetados, .. blocos.Coleta.GruposColetados.SelectMany(static g => g.Subitens)], blocos.Configuracao.Classificacao) is not null
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, "'dadosDosFormularios': a edição do ENEM exige classificação pelo ENEM.")
            : null;

    private static EnvelopeReidratado Montar(
        BlocosDoEnvelope blocos, IReadOnlyList<RegraDeComprovacao> todasAsRegras, IReadOnlyList<FatoColetado> campos, IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas)
    {
        ConfiguracaoPublicada configuracao = ConfiguracaoPublicadaDoEnvelope.Montar(blocos, todasAsRegras, campos, opcoesDeclaradas);
        return new EnvelopeReidratado(
            configuracao, blocos.Configuracao.Dados!, blocos.Configuracao.HashDocumento, blocos.Publicacao.FusoHorario!,
            blocos.Retificacao, blocos.Documentos.Conformidade,
            new DadosDeCatalogoDaConfiguracaoPublicada(
                blocos.Documentos.MetadadosFatosCongelados, blocos.Coleta.ValoresSelecionaveis,
                blocos.Publicacao.CalendarioDiasUteis, blocos.Coleta.AgregadosDosGrupos,
                blocos.Coleta.ApresentacaoDosPressupostos, blocos.Coleta.TiposDeAtoCongelados, blocos.Coleta.FatosDerivadosCongelados));
    }

    /// <summary>
    /// Todo item pertence a um formulário do envelope e está numa seção dele, na ordem das seções: a
    /// mesma recusa da publicação, que nunca congela outra forma.
    /// </summary>
    private static DomainError? ItensForaDosFormularios(
        IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
    {
        Dictionary<Guid, FormularioProcesso> porIdentificador = formularios.ToDictionary(static f => f.Id);
        IEnumerable<(Guid? FormularioId, ItemEstrutura Item)> itensEGrupos = fatos
            .Select(static f => (f.FormularioId, new ItemEstrutura(f.FatoCodigo, f.Ordem, f.PaginaCodigo)))
            .Concat(grupos.Select(static g => (g.FormularioId, g.ParaEstrutura())));
        foreach (IGrouping<Guid?, ItemEstrutura> itens in itensEGrupos.GroupBy(static p => p.FormularioId, static p => p.Item))
        {
            if (ConferirItensDoFormulario(porIdentificador, itens) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    private static DomainError? ConferirItensDoFormulario(Dictionary<Guid, FormularioProcesso> porIdentificador,
        IGrouping<Guid?, ItemEstrutura> itens)
    {
        if (itens.Key is not { } identificador || !porIdentificador.TryGetValue(identificador, out FormularioProcesso? formulario))
        {
            return new DomainError(ErrosCodecEnvelope.EnvelopeMalformado,
                "'dadosDosFormularios' tem itens de um formulário que não está no envelope.");
        }
        return EstruturaFormulario.ValidarItens(formulario.Estrutura, [.. itens]) is [{ } primeiro, ..]
            ? new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, "'dadosDosFormularios': os itens não correspondem à estrutura do formulário.") : null;
    }
}
