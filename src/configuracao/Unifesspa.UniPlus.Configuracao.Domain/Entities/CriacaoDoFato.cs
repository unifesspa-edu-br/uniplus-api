namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// Monta o fato do candidato novo: a factory canônica e os três cadastros do administrador (declarado,
/// derivado por regra e agregado sobre grupo repetível), com os eixos que cada um infere e a
/// conferência do agregado contra o fato de membro. As violações independentes acumulam (ADR-0125).
/// </summary>
internal static class CriacaoDoFato
{
    internal static Result<FatoCandidato> Criar(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        OrigemFato origem,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato,
        string pontoResolucao,
        string binding,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        bool sistema,
        ComponenteDeExibicao? componenteExibicao = null)
    {
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        Result<CodigoFatoCandidato> codigoResult = CodigoFatoCandidato.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }

        Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> descritivo = ValidarDescritivo(nome, descricao, componenteExibicao);
        erros.AddRange(descritivo.Errors);

        ConferirEixosDeValor(erros, dominio, cardinalidade, fonteValores, formato);

        bool origemValida = origem != OrigemFato.Nenhuma && Enum.IsDefined(origem);
        if (origem == OrigemFato.Nenhuma)
        {
            Recusar("origem", FatoCandidatoErrorCodes.OrigemObrigatoria, "Origem do fato é obrigatória.");
        }
        else if (!origemValida)
        {
            Recusar("origem", FatoCandidatoErrorCodes.OrigemInvalida, "Origem do fato fora do vocabulário fechado.");
        }

        Result<string> pontoResolucaoResult = ValidarPontoResolucao(pontoResolucao);
        if (pontoResolucaoResult.IsFailure)
        {
            erros.Add(new("pontoResolucao", pontoResolucaoResult.Error!));
        }

        string? bindingValidado = null;
        if (codigoResult.IsSuccess && origemValida)
        {
            Result<string> bindingResult = ValidarBinding(binding, origem, codigoResult.Value!.Valor);
            bindingValidado = bindingResult.Value;
            if (bindingResult.IsFailure)
            {
                erros.Add(new("binding", bindingResult.Error!));
            }
            else if (!sistema && (origem == OrigemFato.Integracao
                || bindingResult.Value!.StartsWith(PrefixoBindingDerivadoAtributo + ":", StringComparison.Ordinal)
                || bindingResult.Value!.StartsWith(PrefixoBindingDerivadoClassificacao + ":", StringComparison.Ordinal)))
            {
                // O fato do administrador não tem código que calcule ou traga o valor: é declarado,
                // ou derivado pela regra que ele mesmo cadastra. Atributo do candidato, classificação
                // e integração só existem em fato de sistema.
                Recusar(
                    "origem",
                    FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema,
                    "Fato do administrador é declarado ou derivado por regra; atributo do candidato, classificação e integração só existem em fato de sistema.");
            }
        }

        ConferirEscopo(erros, escopo);

        Result<ProtecaoDeDados> protecao = ValidarProtecao(
            dominio, classificacaoProtecao, finalidadeTratamento, hipoteseLegal);
        erros.AddRange(protecao.Errors);

        if (erros.Count > 0)
        {
            return Result<FatoCandidato>.ValidationFailure(erros);
        }

        return Result<FatoCandidato>.Success(new FatoCandidato(
            codigoResult.Value!.Valor,
            descritivo.Value.Nome,
            descritivo.Value.Descricao,
            descritivo.Value.Componente,
            dominio,
            origem,
            cardinalidade,
            fonteValores,
            formato,
            pontoResolucaoResult.Value!,
            bindingValidado!,
            escopo,
            protecao.Value,
            sistema));
    }

    internal static Result<FatoCandidato> CriarDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato,
        string pontoResolucao,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null) =>
        Criar(
            codigo, nome, descricao, dominio, OrigemFato.Declarado, cardinalidade, fonteValores, formato, pontoResolucao,
            $"{PrefixoBindingDeclarado}:{codigo?.Trim()}", escopo, classificacaoProtecao, finalidadeTratamento, hipoteseLegal,
            sistema: false, componenteExibicao);

    internal static Result<FatoCandidato> CriarDerivadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        string pontoResolucao,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null)
    {
        (CardinalidadeFato cardinalidade, FonteValoresFato? fonteValores) = EixosInferidosDoDerivado(dominio);
        Result<FatoCandidato> criado = Criar(
            codigo, nome, descricao, dominio, OrigemFato.Derivado,
            cardinalidade,
            fonteValores,
            formato: null, pontoResolucao, $"{PrefixoBindingDerivadoRegra}:{codigo?.Trim()}", escopo,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false, componenteExibicao);
        List<FieldError> recusasDoDominio = [];
        if (ConferirDominioDoDerivado(recusasDoDominio, dominio))
        {
            return criado;
        }

        // Com o domínio recusado, o que depende dele (formato, fonte) não é conferido.
        return Result<FatoCandidato>.ValidationFailure(
            [.. recusasDoDominio, .. criado.Errors.Where(static e => e.Field is not ("dominio" or "formato" or "fonteValores"))]);
    }

    internal static Result<FatoCandidato> CriarAgregadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        string fatoDeMembroCodigo,
        CatalogoDeFatos catalogo,
        string pontoResolucao,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        string membroCodigo = fatoDeMembroCodigo?.Trim() ?? string.Empty;
        FatoCandidato? membro = catalogo.Fatos.FirstOrDefault(f => string.Equals(f.Codigo, membroCodigo, StringComparison.Ordinal));
        OperacaoAgregado operacao = AgregadoDeGrupo.OperacaoDoDominio(membro is null ? null : DominiosFato.ParaTokenCanonico(membro.Dominio));
        bool campoDeGrupo = membro is { Escopo: EscopoFato.MembroGrupo, Origem: OrigemFato.Declarado };
        if (membro is null)
        {
            Recusar("fatoDeMembro", FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro, $"O fato de membro '{membroCodigo}' não pertence ao catálogo.");
        }
        else if (!campoDeGrupo)
        {
            Recusar("fatoDeMembro", FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo,
                $"O fato '{membroCodigo}' não é fato declarado de membro de grupo: o agregado resume o que cada membro respondeu.");
        }
        else
        {
            if (!membro.Ativo)
            {
                Recusar("fatoDeMembro", VinculoCatalogoErrorCodes.FatoDesativado, $"O fato '{membroCodigo}' está desativado no catálogo e não aceita vínculo novo.");
            }

            if (operacao == OperacaoAgregado.Nenhuma)
            {
                Recusar("fatoDeMembro", FatoCandidatoErrorCodes.AgregadoSobreDominioQueNaoAgrega,
                    "O agregado resume fato de membro booleano (existe membro que…) ou categórico (valores presentes).");
            }
        }

        bool membroValido = campoDeGrupo && operacao != OperacaoAgregado.Nenhuma;
        (DominioFato dominioInferido, CardinalidadeFato cardinalidadeInferida, FonteValoresFato? fonteInferida) =
            EixosInferidosDoAgregado(operacao, membro?.FonteValores);
        Result<FatoCandidato> criado = Criar(
            codigo, nome, descricao,
            dominioInferido,
            OrigemFato.Derivado,
            cardinalidadeInferida,
            fonteInferida,
            formato: null, pontoResolucao, $"{PrefixoBindingAgregacao}:{membroCodigo}", EscopoFato.Candidato,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false, componenteExibicao);

        // As recusas que dependem do fato de membro só valem com ele válido; sem ele, o domínio e a
        // fonte não foram inferidos e as recusas desses campos não orientam.
        erros.AddRange(membroValido ? criado.Errors : criado.Errors.Where(static e => e.Field is not ("dominio" or "fonteValores" or "binding")));
        // A proteção e o ponto de resolução se comparam com os do fato de membro sempre que o campo
        // é válido, independentemente das recusas dos outros campos.
        if (membroValido)
        {
            ConferirContraOMembro(erros, membro!, pontoResolucao, classificacaoProtecao, catalogo);
        }

        return erros.Count > 0 ? Result<FatoCandidato>.ValidationFailure(erros) : criado;
    }

    /// <summary>A cardinalidade e a fonte que o derivado por regra infere do próprio domínio.</summary>
    internal static (CardinalidadeFato Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoDerivado(DominioFato dominio) =>
        dominio == DominioFato.Categorico
            ? (CardinalidadeFato.Multivalorado, FonteValoresFato.Global)
            : (CardinalidadeFato.Escalar, null);

    /// <summary>
    /// O domínio, a cardinalidade e a fonte que o agregado infere da operação sobre o fato de membro:
    /// valores presentes dá categórico multivalorado com a fonte do membro, e o restante dá booleano.
    /// </summary>
    internal static (DominioFato Dominio, CardinalidadeFato Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoAgregado(
        OperacaoAgregado operacao, FonteValoresFato? fonteDoMembro) =>
        operacao == OperacaoAgregado.ValoresPresentes
            ? (DominioFato.Categorico, CardinalidadeFato.Multivalorado, fonteDoMembro)
            : (DominioFato.Booleano, CardinalidadeFato.Escalar, null);

    /// <summary>
    /// O agregado não protege menos nem resolve antes do fato de membro: a recusa vai no campo do
    /// eixo, e só quando o eixo informado é válido.
    /// </summary>
    internal static void ConferirContraOMembro(
        List<FieldError> erros,
        FatoCandidato membro,
        string pontoResolucao,
        ClassificacaoDeProtecao classificacaoProtecao,
        CatalogoDeFatos catalogo)
    {
        if (classificacaoProtecao != ClassificacaoDeProtecao.Nenhuma && Enum.IsDefined(classificacaoProtecao)
            && classificacaoProtecao < membro.ClassificacaoProtecao)
        {
            erros.Add(new("classificacaoProtecao", new DomainError(
                FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia,
                $"O agregado revela o que se sabe de '{membro.Codigo}' e não pode ter proteção de dados mais fraca que a dele.")));
        }

        string ponto = pontoResolucao?.Trim() ?? string.Empty;
        if (FaseCanonicaCatalogo.EhCanonico(ponto) && ValidadorRegrasPadrao.Precede(ponto, membro.PontoResolucao, catalogo.Precedencias))
        {
            erros.Add(new("pontoResolucao", new DomainError(
                FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia,
                $"O agregado resolve em {ponto}, antes de '{membro.Codigo}', que só é conhecido em {membro.PontoResolucao}.")));
        }
    }
}
