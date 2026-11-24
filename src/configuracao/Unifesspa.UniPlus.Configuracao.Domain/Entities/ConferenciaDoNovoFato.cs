namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// As conferências do fato novo, na ordem do cadastro: código, apresentação, eixos de valor, origem, ponto de
/// resolução, vínculo, escopo e proteção de dados. As violações independentes acumulam (ADR-0125); a que depende de
/// outro campo só é conferida quando esse campo é válido. Não grava nada: devolve o fato montado ou as recusas.
/// </summary>
internal sealed class ConferenciaDoNovoFato(
    DescritivoDoFato descritivo, OrigemFato? origem, string binding, DefinicaoDoFato definicao, bool sistema)
{
    // O fato do administrador não tem código que calcule ou traga o valor: é declarado, ou calculado
    // pela regra que ele mesmo cadastra. Atributo do candidato, classificação e integração só existem
    // em fato de sistema.
    private static readonly FieldError VinculoExclusivoDeSistemaRecusado = new(
        "origem",
        new DomainError(
            FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema,
            "Fato do administrador é declarado ou calculado por regra; atributo do candidato, classificação e integração só existem em fato de sistema."));

    private readonly List<FieldError> _erros = [];

    internal Result<FatoCandidato> Conferir()
    {
        Result<CodigoFatoCandidato> codigo = ConferirCodigo();
        Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> apresentacao = ConferirApresentacao();
        ConferirEixosDeValor(_erros, definicao.Dominio, definicao.Cardinalidade, definicao.FonteValores, definicao.Formato);
        bool origemValida = ConferirOrigem();
        Result<string> ponto = ConferirPontoDeResolucao();
        string? vinculo = codigo.IsSuccess && origemValida ? ConferirVinculo(codigo.Value!.Valor) : null;
        ConferirEscopo(_erros, definicao.Escopo ?? EscopoFato.Nenhum);
        Result<ProtecaoDeDados> protecao = ConferirProtecao();

        return _erros.Count > 0
            ? Result<FatoCandidato>.ValidationFailure(_erros)
            : Result<FatoCandidato>.Success(Montar(codigo.Value!.Valor, apresentacao.Value, ponto.Value!, vinculo!, protecao.Value));
    }

    private Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> ConferirApresentacao()
    {
        Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> apresentacao = ValidarDescritivo(
            descritivo.Nome, descritivo.Descricao, descritivo.ComponenteExibicao);
        _erros.AddRange(apresentacao.Errors);
        return apresentacao;
    }

    private Result<string> ConferirPontoDeResolucao()
    {
        Result<string> ponto = ValidarPontoResolucao(definicao.PontoResolucao);
        _erros.AddRange(Presentes(ponto.IsFailure ? new FieldError("pontoResolucao", ponto.Error!) : null));
        return ponto;
    }

    private Result<ProtecaoDeDados> ConferirProtecao()
    {
        Result<ProtecaoDeDados> protecao = ValidarProtecao(
            definicao.Dominio, definicao.ClassificacaoProtecao, definicao.FinalidadeTratamento, definicao.HipoteseLegal);
        _erros.AddRange(protecao.Errors);
        return protecao;
    }

    private Result<CodigoFatoCandidato> ConferirCodigo()
    {
        Result<CodigoFatoCandidato> codigo = CodigoFatoCandidato.Criar(descritivo.Codigo);
        _erros.AddRange(Presentes(codigo.IsFailure ? new FieldError("codigo", codigo.Error!) : null));
        return codigo;
    }

    private bool ConferirOrigem()
    {
        (string Codigo, string Mensagem)? recusa = origem switch
        {
            null => (FatoCandidatoErrorCodes.OrigemObrigatoria, "Origem do fato é obrigatória."),
            { } informada when !Enum.IsDefined(informada) => (FatoCandidatoErrorCodes.OrigemInvalida, "Origem do fato fora do vocabulário fechado."),
            _ => null,
        };

        return RegistrarRecusa(_erros, "origem", recusa);
    }

    private string? ConferirVinculo(string codigo)
    {
        Result<string> resultado = ValidarBinding(binding, origem!.Value, codigo);
        if (resultado.IsFailure)
        {
            _erros.Add(new("binding", resultado.Error!));
        }
        else if (!sistema && VinculoExclusivoDeFatoDeSistema(resultado.Value!))
        {
            _erros.Add(VinculoExclusivoDeSistemaRecusado);
        }

        return resultado.Value;
    }

    private bool VinculoExclusivoDeFatoDeSistema(string vinculo) =>
        origem == OrigemFato.Integracao
        || VinculoDeFato.Usa(vinculo, MecanismoDoVinculo.AtributoCandidato)
        || VinculoDeFato.Usa(vinculo, MecanismoDoVinculo.Classificacao);

    private FatoCandidato Montar(
        string codigo,
        (string Nome, string Descricao, ComponenteDeExibicao? Componente) apresentacao,
        string ponto,
        string vinculo,
        ProtecaoDeDados protecao) =>
        new(
            new DescritivoDoFato(codigo, apresentacao.Nome, apresentacao.Descricao, apresentacao.Componente),
            origem!.Value,
            vinculo,
            new EixosDaDefinicao(
                definicao.Dominio!.Value, definicao.Cardinalidade!.Value, definicao.FonteValores, definicao.Formato, ponto, definicao.Escopo,
                protecao.Classificacao, protecao.Finalidade, protecao.Hipotese),
            sistema);
}
