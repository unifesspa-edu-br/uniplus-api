namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;
using static Unifesspa.UniPlus.Configuracao.Domain.Entities.CriacaoDoFato;

/// <summary>
/// Os eixos que a alteração da definição do fato do administrador propõe, com as recusas do cadastro
/// (ADR-0125): o declarado aceita todos; o derivado por regra infere cardinalidade e fonte do domínio;
/// o agregado infere tudo do fato de membro e só troca o ponto de resolução e a proteção. Não grava
/// nada: devolve a proposta, e o agregado a aplica.
/// </summary>
internal sealed class ConferenciaDaRedefinicaoDoFato
{
    private const string MensagemDoAgregado =
        "O agregado infere este eixo do fato de membro; só o ponto de resolução e a proteção de dados se alteram.";

    private const string MensagemDoDerivado = "O fato derivado por regra infere este eixo do domínio; não o informe.";

    private const string MensagemDeDominioSemValores =
        "O fato já tem valores cadastrados e só o categórico os tem; desativar os valores não basta, porque o código de um valor nunca é removido.";

    private const string MensagemDeFonteSemValores = "O fato já tem valores cadastrados no catálogo e só a fonte global os tem.";

    private readonly DefinicaoDoFato _definicao;
    private readonly FatoCandidato _fato;
    private readonly List<FieldError> _erros = [];
    private TipoDoDado? _dominio;
    private QuantidadeDeValores? _cardinalidade;
    private FonteValoresFato? _fonteValores;
    private FormatoTexto? _formato;
    private EscopoFato? _escopo;
    private bool _eixosDeValorValidos = true;

    // Só o domínio aplicável confere a proteção mínima: o recusado (fora do vocabulário, ou texto num
    // derivado) nunca será gravado, e a recusa dele já foi dada.
    private bool _dominioAplicavel = true;

    internal ConferenciaDaRedefinicaoDoFato(DefinicaoDoFato definicao, FatoCandidato fato)
    {
        _definicao = definicao;
        _fato = fato;
        _dominio = fato.Dominio;
        _cardinalidade = fato.Cardinalidade;
        _fonteValores = fato.FonteValores;
        _formato = fato.Formato;
        _escopo = fato.Escopo;
    }

    internal PropostaDaRedefinicao Propor()
    {
        if (_fato.FatoDeMembroAgregado is not null)
        {
            RecusarEixosDoAgregado();
        }
        else
        {
            ConferirEixos();
        }

        return Proposta(ConferirPontoDeResolucao(), ConferirProtecao());
    }

    private Result<string> ConferirPontoDeResolucao()
    {
        Result<string> ponto = ValidarPontoResolucao(_definicao.PontoResolucao);
        _erros.AddRange(Presentes(ponto.IsFailure ? new FieldError("pontoResolucao", ponto.Error!) : null));
        return ponto;
    }

    private Result<ProtecaoDeDados> ConferirProtecao()
    {
        Result<ProtecaoDeDados> protecao = ValidarProtecao(
            _dominioAplicavel ? _dominio : null, _definicao.ClassificacaoProtecao, _definicao.FinalidadeTratamento, _definicao.HipoteseLegal);
        _erros.AddRange(protecao.Errors);
        return protecao;
    }

    // A coerência com as dependências do próprio fato é conferida com o que há de válido na entrada; o
    // eixo recusado fica como está, e a recusa dele já foi dada.
    private PropostaDaRedefinicao Proposta(Result<string> ponto, Result<ProtecaoDeDados> protecao)
    {
        EixosDaDefinicao atuais = _fato.Eixos;
        EixosDaDefinicao propostos = new(
            Conferido(_dominio, atuais.Dominio),
            Conferido(_cardinalidade, atuais.Cardinalidade),
            _eixosDeValorValidos ? _fonteValores : atuais.FonteValores,
            _eixosDeValorValidos ? _formato : atuais.Formato,
            ponto.IsSuccess ? ponto.Value! : atuais.PontoResolucao,
            _escopo,
            protecao.IsSuccess ? protecao.Value.Classificacao : atuais.Classificacao,
            protecao.IsSuccess ? protecao.Value.Finalidade : atuais.Finalidade,
            protecao.IsSuccess ? protecao.Value.Hipotese : atuais.Hipotese);
        return new PropostaDaRedefinicao(propostos, _erros, ponto.IsSuccess, protecao.IsSuccess);
    }

    private T Conferido<T>(T? proposto, T atual)
        where T : struct =>
        _eixosDeValorValidos ? proposto ?? atual : atual;

    private void RecusarEixosDoAgregado()
    {
        RecusarSeInformado("dominio", _definicao.Dominio.Informado, MensagemDoAgregado);
        RecusarSeInformado("cardinalidade", _definicao.Cardinalidade.Informado, MensagemDoAgregado);
        RecusarSeInformado("fonteValores", _definicao.FonteValores.Informado, MensagemDoAgregado);
        RecusarSeInformado("formato", _definicao.Formato.Informado, MensagemDoAgregado);
        RecusarSeInformado("escopo", _definicao.Escopo.Informado, MensagemDoAgregado);
    }

    private void ConferirEixos()
    {
        _dominio = _definicao.Dominio.Valor;
        _escopo = _definicao.Escopo.Valor;
        ConferirEscopo(_erros, _definicao.Escopo);
        if (_fato.DerivadoPorRegra)
        {
            ConferirEixosDoDerivado();
        }
        else
        {
            ConferirEixosDeValorInformados();
        }

        RecusarValoresQueOEixoNaoComporta();
    }

    private void ConferirEixosDoDerivado()
    {
        RecusarSeInformado("cardinalidade", _definicao.Cardinalidade.Informado, MensagemDoDerivado);
        RecusarSeInformado("fonteValores", _definicao.FonteValores.Informado, MensagemDoDerivado);
        RecusarSeInformado("formato", _definicao.Formato.Informado, MensagemDoDerivado);
        (_cardinalidade, _fonteValores) = EixosInferidosDoDerivado(_dominio);
        _formato = null;
        _eixosDeValorValidos = _dominioAplicavel = ConferirDominioDoDerivado(_erros, _definicao.Dominio);
    }

    private void ConferirEixosDeValorInformados()
    {
        _cardinalidade = _definicao.Cardinalidade.Valor;
        _fonteValores = _definicao.FonteValores.Valor;
        _formato = _definicao.Formato.Valor;
        int antes = _erros.Count;
        ConferirEixosDeValor(_erros, _definicao.Dominio, _definicao.Cardinalidade, _definicao.FonteValores, _definicao.Formato);
        _eixosDeValorValidos = _erros.Count == antes;
        _dominioAplicavel = _dominio is not null;
    }

    // Os valores do catálogo só existem no categórico de fonte global: o eixo novo que não os
    // comporta é recusado, e o valor não é descartado em silêncio.
    private void RecusarValoresQueOEixoNaoComporta()
    {
        if (!_eixosDeValorValidos || _fato.ValoresDominioDeclarados.Count == 0)
        {
            return;
        }
        if (_dominio != TipoDoDado.Opcao)
        {
            RecusarEixoSemValores("dominio", FatoCandidatoErrorCodes.DominioNaoComportaOsValores, MensagemDeDominioSemValores);
        }
        else if (_fonteValores != FonteValoresFato.Catalogo)
        {
            RecusarEixoSemValores("fonteValores", FatoCandidatoErrorCodes.FonteNaoComportaOsValores, MensagemDeFonteSemValores);
        }
    }

    private void RecusarEixoSemValores(string campo, string codigo, string mensagem)
    {
        _eixosDeValorValidos = false;
        Recusar(campo, codigo, mensagem);
    }

    private void RecusarSeInformado(string campo, bool informado, string mensagem)
    {
        if (informado)
        {
            Recusar(campo, FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato, mensagem);
        }
    }

    private void Recusar(string campo, string codigo, string mensagem) =>
        _erros.Add(new FieldError(campo, new DomainError(codigo, mensagem)));
}
