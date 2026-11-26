namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As conferências que o cadastro e a alteração do fato do candidato fazem do mesmo jeito (ADR-0125):
/// descritivo, eixos de valor, escopo, ponto de resolução, vínculo e proteção de dados. Só confere e
/// devolve as recusas; quem grava é o agregado.
/// </summary>
internal static class ConferenciaDaDefinicaoDoFato
{
    internal const int NomeMaxLength = 200;
    internal const int DescricaoMaxLength = 1000;
    internal const int BindingMaxLength = 200;

    // Um fato derivado tem três mecanismos de produção de valor: computar de um atributo do
    // candidato (FAIXA_ETARIA, RENDA_PER_CAPITA), referenciar a regra de derivação congelada do
    // processo (MODALIDADE) ou receber o resultado da classificação (MODALIDADE_CONVOCACAO, o grupo
    // em que o candidato foi convocado). O catálogo é global e diz o mecanismo; a config do edital
    // diz o conteúdo. Declarado e Integracao seguem com um prefixo cada (ADR-0116).
    private static readonly Dictionary<OrigemFato, IReadOnlyList<MecanismoDoVinculo>> MecanismosPorOrigem =
        new()
        {
            [OrigemFato.Calculado] =
                [MecanismoDoVinculo.AtributoCandidato, MecanismoDoVinculo.RegraDeDerivacao, MecanismoDoVinculo.Classificacao, MecanismoDoVinculo.ResumoDeGrupo],
            [OrigemFato.Declarado] = [MecanismoDoVinculo.CampoFormulario],
            [OrigemFato.Integracao] = [MecanismoDoVinculo.Integracao],
        };

    /// <summary>
    /// O tipo do dado, a quantidade de valores, a fonte dos valores e o formato, que o cadastro e a alteração da
    /// definição conferem do mesmo jeito (ADR-0125): as violações acumulam, e a que depende do
    /// tipo do dado só é conferida com o tipo válido.
    /// </summary>
    internal static void ConferirEixosDeValor(
        List<FieldError> erros,
        CodigoInformado<TipoDoDado> tipo,
        CodigoInformado<QuantidadeDeValores> quantidade,
        CodigoInformado<FonteValoresFato> fonteValores,
        CodigoInformado<FormatoTexto> formato)
    {
        bool tipoValido = RegistrarRecusa(erros, "dominio", RecusaDoDominio(tipo));
        ConferirQuantidadeDeValores(erros, quantidade);

        TipoDoDado? tipoConferido = tipoValido ? tipo.Valor : null;
        ConferirFonteDosValores(erros, tipoConferido, fonteValores);
        ConferirFormato(erros, tipoConferido, formato);
    }

    // O tipo do dado ausente é obrigatório e o desconhecido é inválido; o conhecido não tem recusa aqui.
    private static (string Codigo, string Mensagem)? RecusaDoDominio(CodigoInformado<TipoDoDado> tipo) => tipo switch
    {
        { Informado: false } => (FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório."),
        { Desconhecido: true } => (FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado."),
        _ => null,
    };

    private static void ConferirQuantidadeDeValores(List<FieldError> erros, CodigoInformado<QuantidadeDeValores> quantidade)
    {
        (string Codigo, string Mensagem)? recusa = quantidade switch
        {
            { Informado: false } => (FatoCandidatoErrorCodes.CardinalidadeObrigatoria, "Cardinalidade do fato é obrigatória."),
            { Desconhecido: true } => (FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado."),
            _ => null,
        };

        RegistrarRecusa(erros, "cardinalidade", recusa);
    }

    private static void ConferirFonteDosValores(List<FieldError> erros, TipoDoDado? tipoConferido, CodigoInformado<FonteValoresFato> fonteValores)
    {
        (string Codigo, string Mensagem)? recusa = (tipoConferido, fonteValores) switch
        {
            (_, { Desconhecido: true }) =>
                (FatoCandidatoErrorCodes.FonteValoresInvalida, "Fonte dos valores do fato fora do vocabulário fechado."),
            (TipoDoDado.Opcao, { Informado: false }) =>
                (FatoCandidatoErrorCodes.FonteValoresObrigatoria, "Fato categórico precisa declarar a fonte dos seus valores."),
            ({ } tipo, { Informado: true }) when tipo != TipoDoDado.Opcao =>
                (FatoCandidatoErrorCodes.FonteValoresForaDeCategorico, "Só fato categórico declara a fonte dos seus valores."),
            _ => null,
        };

        RegistrarRecusa(erros, "fonteValores", recusa);
    }

    private static void ConferirFormato(List<FieldError> erros, TipoDoDado? tipoConferido, CodigoInformado<FormatoTexto> formato)
    {
        (string Codigo, string Mensagem)? recusa = (tipoConferido, formato) switch
        {
            (_, { Desconhecido: true }) =>
                (FatoCandidatoErrorCodes.FormatoInvalido, "Formato do fato fora do vocabulário fechado."),
            (TipoDoDado.Texto, { Informado: false }) =>
                (FatoCandidatoErrorCodes.FormatoObrigatorio, "Fato de domínio texto precisa declarar o formato."),
            ({ } tipo, { Informado: true }) when tipo != TipoDoDado.Texto =>
                (FatoCandidatoErrorCodes.FormatoForaDeTexto, "Só fato de domínio texto declara formato."),
            _ => null,
        };

        RegistrarRecusa(erros, "formato", recusa);
    }

    internal static bool RegistrarRecusa(List<FieldError> erros, string campo, (string Codigo, string Mensagem)? recusa)
    {
        if (recusa is { } r)
        {
            erros.Add(new(campo, new DomainError(r.Codigo, r.Mensagem)));
        }

        return recusa is null;
    }

    internal static void ConferirEscopo(List<FieldError> erros, CodigoInformado<EscopoFato> escopo)
    {
        (string Codigo, string Mensagem)? recusa = escopo switch
        {
            { Informado: false } => (FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato é obrigatório."),
            { Desconhecido: true } => (FatoCandidatoErrorCodes.EscopoInvalido, "Escopo do fato fora do vocabulário fechado."),
            _ => null,
        };
        RegistrarRecusa(erros, "escopo", recusa);
    }

    internal static Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> ValidarDescritivo(
        string nome, string? descricao, CodigoInformado<ComponenteDeExibicao> componente)
    {
        List<FieldError> erros = [];
        string nomeNormalizado = ConferirNome(erros, nome);
        string descricaoNormalizada = ConferirDescricao(erros, descricao);
        ConferirComponente(erros, componente);

        return erros.Count > 0
            ? Result<(string, string, ComponenteDeExibicao?)>.ValidationFailure(erros)
            : Result<(string, string, ComponenteDeExibicao?)>.Success((nomeNormalizado, descricaoNormalizada, componente.Valor));
    }

    private static string ConferirNome(List<FieldError> erros, string? nome)
    {
        string normalizado = nome?.Trim() ?? string.Empty;
        (string Codigo, string Mensagem)? recusa = normalizado.Length switch
        {
            0 => (FatoCandidatoErrorCodes.NomeObrigatorio, "Nome do fato é obrigatório."),
            > NomeMaxLength => (FatoCandidatoErrorCodes.NomeTamanho, $"Nome do fato deve ter no máximo {NomeMaxLength} caracteres."),
            _ => null,
        };

        RegistrarRecusa(erros, "nome", recusa);
        return normalizado;
    }

    private static string ConferirDescricao(List<FieldError> erros, string? descricao)
    {
        string normalizada = descricao?.Trim() ?? string.Empty;
        (string Codigo, string Mensagem)? recusa = normalizada.Length switch
        {
            0 => (FatoCandidatoErrorCodes.DescricaoObrigatoria, "Descrição do fato é obrigatória."),
            > DescricaoMaxLength => (FatoCandidatoErrorCodes.DescricaoTamanho, $"Descrição do fato deve ter no máximo {DescricaoMaxLength} caracteres."),
            _ => null,
        };

        RegistrarRecusa(erros, "descricao", recusa);
        return normalizada;
    }

    private static void ConferirComponente(List<FieldError> erros, CodigoInformado<ComponenteDeExibicao> componente)
    {
        (string Codigo, string Mensagem)? recusa = componente.Desconhecido
            ? (FatoCandidatoErrorCodes.ComponenteExibicaoInvalido, "Componente de exibição do fato fora do vocabulário fechado.")
            : null;

        RegistrarRecusa(erros, "componenteExibicao", recusa);
    }

    /// <summary>
    /// O tipo do dado que o derivado por regra aceita, no cadastro e na alteração: ausente é obrigatório,
    /// fora do vocabulário é inválido e o válido que não é sim ou não nem opção não tem regra.
    /// </summary>
    internal static bool ConferirDominioDoDerivado(List<FieldError> erros, CodigoInformado<TipoDoDado> tipo)
    {
        (string Codigo, string Mensagem)? recusa = tipo.Valor switch
        {
            TipoDoDado.SimOuNao or TipoDoDado.Opcao => null,
            null => RecusaDoDominio(tipo),
            _ => (FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico, "Fato derivado por regra é booleano ou categórico."),
        };

        return RegistrarRecusa(erros, "dominio", recusa);
    }

    private static readonly RecusasDaProtecao RecusasDaProtecaoDoFato = new(
        "do fato",
        FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria,
        FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida,
        FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria,
        FatoCandidatoErrorCodes.FinalidadeTratamentoTamanho,
        FatoCandidatoErrorCodes.HipoteseLegalObrigatoria,
        FatoCandidatoErrorCodes.HipoteseLegalInvalida,
        FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao);

    internal static Result<ProtecaoDeDados> ValidarProtecao(
        TipoDoDado? tipo,
        CodigoInformado<ClassificacaoDeProtecao> classificacao,
        string finalidade,
        CodigoInformado<HipoteseLegalTratamento> hipotese) =>
        ProtecaoDeDados.Validar(
            classificacao, finalidade, hipotese, RecusasDaProtecaoDoFato,
            // Texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), data e
            // endereço identificam ou localizam a pessoa: nunca são menos que dado pessoal. Texto
            // também aceita `Identificador` — número de documento é sempre texto, nunca outro
            // domínio (ADR-0136).
            nivel => tipo is { } tipoConferido && !AtendeClassificacaoMinimaDoTipo(tipoConferido, nivel)
                ? new DomainError(
                    FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio,
                    "Classificação de proteção de dados incompatível com o domínio do fato.")
                : null);

    private static bool AtendeClassificacaoMinimaDoTipo(TipoDoDado tipo, ClassificacaoDeProtecao classificacao) =>
        tipo switch
        {
            TipoDoDado.Texto => classificacao is ClassificacaoDeProtecao.Pessoal
                or ClassificacaoDeProtecao.Identificador or ClassificacaoDeProtecao.Sensivel,
            TipoDoDado.Data or TipoDoDado.Endereco => classificacao is ClassificacaoDeProtecao.Pessoal
                or ClassificacaoDeProtecao.Sensivel,
            // Identificador é só para texto — número de documento nunca é categórico, booleano
            // nem numérico. Sem essa recusa, um categórico Identificador citado como membro de
            // agregado elevaria a classificação mínima do derivado (ADR-0136).
            _ => classificacao != ClassificacaoDeProtecao.Identificador,
        };

    internal static Result<string> ValidarPontoResolucao(string pontoResolucao)
    {
        string normalizado = pontoResolucao?.Trim() ?? string.Empty;
        DomainError? recusa = normalizado.Length == 0
            ? new DomainError(FatoCandidatoErrorCodes.PontoResolucaoObrigatorio, "Ponto de resolução do fato é obrigatório.")
            : RecusaDaFaseDoPonto(normalizado);

        return recusa is null ? Result<string>.Success(normalizado) : Result<string>.Failure(recusa);
    }

    private static DomainError? RecusaDaFaseDoPonto(string normalizado) =>
        FaseCanonicaCatalogo.EhCanonico(normalizado)
            ? null
            : new DomainError(FatoCandidatoErrorCodes.PontoResolucaoInvalido, "Ponto de resolução do fato fora do conjunto canônico de fases.");

    internal static Result<string> ValidarBinding(string binding, OrigemFato origem, string codigo)
    {
        string normalizado = binding?.Trim() ?? string.Empty;
        DomainError? recusa = RecusaDoFormatoDoBinding(normalizado)
            ?? RecusaDoPrefixoDoBinding(normalizado, origem)
            ?? RecusaDaReferenciaDaRegra(normalizado, codigo);

        return recusa is null ? Result<string>.Success(normalizado) : Result<string>.Failure(recusa);
    }

    private static DomainError? RecusaDoFormatoDoBinding(string normalizado)
    {
        (string Codigo, string Mensagem)? recusa = normalizado switch
        {
            { Length: 0 } => (FatoCandidatoErrorCodes.BindingObrigatorio, "Binding do fato é obrigatório."),
            { Length: > BindingMaxLength } => (
                FatoCandidatoErrorCodes.BindingFormatoInvalido, $"Binding do fato deve ter no máximo {BindingMaxLength} caracteres."),
            _ when !TemPrefixoEReferencia(normalizado) => (
                FatoCandidatoErrorCodes.BindingFormatoInvalido,
                "Binding deve seguir o formato \"{PREFIXO}:{REFERENCIA}\", com prefixo e referência não vazios."),
            _ => null,
        };

        return recusa is { } r ? new DomainError(r.Codigo, r.Mensagem) : null;
    }

    private static bool TemPrefixoEReferencia(string normalizado)
    {
        int separador = normalizado.IndexOf(':', StringComparison.Ordinal);
        return separador > 0 && separador != normalizado.Length - 1;
    }

    private static DomainError? RecusaDoPrefixoDoBinding(string normalizado, OrigemFato origem)
    {
        string prefixo = PrefixoDoBinding(normalizado);
        string[] prefixosAceitos = [.. MecanismosAceitos(origem).Select(static mecanismo => mecanismo.Codigo())];
        if (prefixosAceitos.Contains(prefixo, StringComparer.Ordinal))
        {
            return null;
        }

        string esperado = prefixosAceitos.Length == 1
            ? $"\"{prefixosAceitos[0]}\""
            : $"um de {string.Join(", ", prefixosAceitos.Select(p => $"\"{p}\""))}";
        return new DomainError(
            FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem,
            $"O prefixo do binding deve ser {esperado} para a origem informada.");
    }

    // REGRA_DE_CALCULO deve apontar para o próprio dado, para que a cópia gravada
    // não descreva o cálculo de outro dado.
    private static DomainError? RecusaDaReferenciaDaRegra(string normalizado, string codigo)
    {
        string prefixo = PrefixoDoBinding(normalizado);
        string referencia = normalizado[(prefixo.Length + 1)..];
        string prefixoDaRegra = MecanismoDoVinculo.RegraDeDerivacao.Codigo();
        return string.Equals(prefixo, prefixoDaRegra, StringComparison.Ordinal)
            && !string.Equals(referencia, codigo, StringComparison.Ordinal)
            ? new DomainError(
                FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente,
                $"O binding \"{prefixoDaRegra}:\" referencia a regra de derivação do próprio fato — "
                + "a referência deve ser o código do próprio fato.")
            : null;
    }

    private static string PrefixoDoBinding(string normalizado) => normalizado[..normalizado.IndexOf(':', StringComparison.Ordinal)];

    private static IReadOnlyList<MecanismoDoVinculo> MecanismosAceitos(OrigemFato origem) =>
        MecanismosPorOrigem.TryGetValue(origem, out IReadOnlyList<MecanismoDoVinculo>? mecanismos)
            ? mecanismos
            : throw new ArgumentOutOfRangeException(nameof(origem), origem, "Origem de fato fora do domínio fechado.");

    /// <summary>As recusas que existem entre as conferidas, na ordem em que foram dadas.</summary>
    internal static IEnumerable<FieldError> Presentes(params FieldError?[] recusas) => recusas.OfType<FieldError>();
}
