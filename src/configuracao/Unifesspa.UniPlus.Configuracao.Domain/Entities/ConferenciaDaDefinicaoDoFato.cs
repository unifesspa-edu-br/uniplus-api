namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
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

    internal const string PrefixoBindingDerivadoAtributo = VinculoDeFato.AtributoDoCandidato;
    internal const string PrefixoBindingDerivadoRegra = VinculoDeFato.RegraDeDerivacao;
    internal const string PrefixoBindingDerivadoClassificacao = VinculoDeFato.Classificacao;
    internal const string PrefixoBindingDeclarado = VinculoDeFato.CampoDoFormulario;
    internal const string PrefixoBindingIntegracao = VinculoDeFato.Integracao;
    internal const string PrefixoBindingAgregacao = VinculoDeFato.AgregacaoDeGrupo;

    // Um fato derivado tem três mecanismos de produção de valor: computar de um atributo do
    // candidato (FAIXA_ETARIA, RENDA_PER_CAPITA), referenciar a regra de derivação congelada do
    // processo (MODALIDADE) ou receber o resultado da classificação (MODALIDADE_CONVOCACAO, o grupo
    // em que o candidato foi convocado). O catálogo é global e diz o mecanismo; a config do edital
    // diz o conteúdo. Declarado e Integracao seguem com um prefixo cada (ADR-0116).
    private static readonly Dictionary<OrigemFato, IReadOnlyList<string>> PrefixosBindingPorOrigem =
        new()
        {
            [OrigemFato.Derivado] =
                [PrefixoBindingDerivadoAtributo, PrefixoBindingDerivadoRegra, PrefixoBindingDerivadoClassificacao, PrefixoBindingAgregacao],
            [OrigemFato.Declarado] = [PrefixoBindingDeclarado],
            [OrigemFato.Integracao] = [PrefixoBindingIntegracao],
        };

    /// <summary>
    /// O domínio, a cardinalidade, a fonte dos valores e o formato, que o cadastro e a alteração da
    /// definição conferem do mesmo jeito (ADR-0125): as violações acumulam, e a que depende do
    /// domínio só é conferida com o domínio válido.
    /// </summary>
    internal static void ConferirEixosDeValor(
        List<FieldError> erros,
        DominioFato dominio,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato)
    {
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        bool dominioValido = dominio != DominioFato.Nenhum && Enum.IsDefined(dominio);
        if (dominio == DominioFato.Nenhum)
        {
            Recusar("dominio", FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório.");
        }
        else if (!dominioValido)
        {
            Recusar("dominio", FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado.");
        }

        if (cardinalidade == CardinalidadeFato.Nenhuma)
        {
            Recusar("cardinalidade", FatoCandidatoErrorCodes.CardinalidadeObrigatoria, "Cardinalidade do fato é obrigatória.");
        }
        else if (!Enum.IsDefined(cardinalidade))
        {
            Recusar("cardinalidade", FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado.");
        }

        if (dominioValido)
        {
            bool ehCategorico = dominio == DominioFato.Categorico;
            if (ehCategorico && (fonteValores is null or FonteValoresFato.Nenhuma || !Enum.IsDefined(fonteValores.Value)))
            {
                Recusar("fonteValores", FatoCandidatoErrorCodes.FonteValoresObrigatoria, "Fato categórico precisa declarar a fonte dos seus valores.");
            }
            else if (!ehCategorico && fonteValores is not null)
            {
                Recusar("fonteValores", FatoCandidatoErrorCodes.FonteValoresForaDeCategorico, "Só fato categórico declara a fonte dos seus valores.");
            }

            bool ehTexto = dominio == DominioFato.Texto;
            if (ehTexto && (formato is null or FormatoTexto.Nenhum || !Enum.IsDefined(formato.Value)))
            {
                Recusar("formato", FatoCandidatoErrorCodes.FormatoObrigatorio, "Fato de domínio texto precisa declarar o formato.");
            }
            else if (!ehTexto && formato is not null)
            {
                Recusar("formato", FatoCandidatoErrorCodes.FormatoForaDeTexto, "Só fato de domínio texto declara formato.");
            }
        }
    }

    internal static void ConferirEscopo(List<FieldError> erros, EscopoFato escopo)
    {
        if (escopo == EscopoFato.Nenhum || !Enum.IsDefined(escopo))
        {
            erros.Add(new("escopo", new DomainError(FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato é obrigatório.")));
        }
    }

    internal static Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> ValidarDescritivo(
        string nome, string? descricao, ComponenteDeExibicao? componente)
    {
        List<FieldError> erros = [];
        string nomeNormalizado = nome?.Trim() ?? string.Empty;
        if (nomeNormalizado.Length == 0)
        {
            erros.Add(new("nome", new DomainError(FatoCandidatoErrorCodes.NomeObrigatorio, "Nome do fato é obrigatório.")));
        }
        else if (nomeNormalizado.Length > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                FatoCandidatoErrorCodes.NomeTamanho, $"Nome do fato deve ter no máximo {NomeMaxLength} caracteres.")));
        }

        string descricaoNormalizada = descricao?.Trim() ?? string.Empty;
        if (descricaoNormalizada.Length == 0)
        {
            erros.Add(new("descricao", new DomainError(
                FatoCandidatoErrorCodes.DescricaoObrigatoria, "Descrição do fato é obrigatória.")));
        }
        else if (descricaoNormalizada.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                FatoCandidatoErrorCodes.DescricaoTamanho, $"Descrição do fato deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        if (componente is { } informado && (informado == ComponenteDeExibicao.Nenhum || !Enum.IsDefined(informado)))
        {
            erros.Add(new("componenteExibicao", new DomainError(
                FatoCandidatoErrorCodes.ComponenteExibicaoInvalido, "Componente de exibição do fato fora do vocabulário fechado.")));
        }

        return erros.Count > 0
            ? Result<(string, string, ComponenteDeExibicao?)>.ValidationFailure(erros)
            : Result<(string, string, ComponenteDeExibicao?)>.Success((nomeNormalizado, descricaoNormalizada, componente));
    }

    /// <summary>
    /// O domínio que o derivado por regra aceita, no cadastro e na alteração: ausente é obrigatório,
    /// fora do vocabulário é inválido e o válido que não é booleano nem categórico não tem regra.
    /// </summary>
    internal static bool ConferirDominioDoDerivado(List<FieldError> erros, DominioFato dominio)
    {
        (string Codigo, string Mensagem)? recusa = dominio switch
        {
            DominioFato.Booleano or DominioFato.Categorico => null,
            DominioFato.Nenhum => (FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório."),
            _ when !Enum.IsDefined(dominio) => (FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado."),
            _ => (FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico, "Fato derivado por regra é booleano ou categórico."),
        };
        if (recusa is { } r)
        {
            erros.Add(new("dominio", new DomainError(r.Codigo, r.Mensagem)));
        }

        return recusa is null;
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
        DominioFato? dominio, ClassificacaoDeProtecao classificacao, string finalidade, HipoteseLegalTratamento hipotese) =>
        ProtecaoDeDados.Validar(
            classificacao, finalidade, hipotese, RecusasDaProtecaoDoFato,
            // Texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), data e
            // endereço identificam ou localizam a pessoa: nunca são menos que dado pessoal. Texto
            // também aceita `Identificador` — número de documento é sempre texto, nunca outro
            // domínio (ADR-0136, emenda de #1857).
            nivel => dominio is { } dominioConferido && !AtendeClassificacaoMinimaDoDominio(dominioConferido, nivel)
                ? new DomainError(
                    FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio,
                    "Classificação de proteção de dados incompatível com o domínio do fato.")
                : null);

    private static bool AtendeClassificacaoMinimaDoDominio(DominioFato dominio, ClassificacaoDeProtecao classificacao) =>
        dominio switch
        {
            DominioFato.Texto => classificacao is ClassificacaoDeProtecao.Pessoal
                or ClassificacaoDeProtecao.Identificador or ClassificacaoDeProtecao.Sensivel,
            DominioFato.Data or DominioFato.Endereco => classificacao is ClassificacaoDeProtecao.Pessoal
                or ClassificacaoDeProtecao.Sensivel,
            // Identificador é só para texto — número de documento nunca é categórico, booleano
            // nem numérico. Sem essa recusa, um categórico Identificador citado como membro de
            // agregado elevaria a classificação mínima do derivado (ADR-0136, emenda de #1857).
            _ => classificacao != ClassificacaoDeProtecao.Identificador,
        };

    internal static Result<string> ValidarPontoResolucao(string pontoResolucao)
    {
        if (string.IsNullOrWhiteSpace(pontoResolucao))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.PontoResolucaoObrigatorio,
                "Ponto de resolução do fato é obrigatório."));
        }

        string normalizado = pontoResolucao.Trim();
        if (!FaseCanonicaCatalogo.EhCanonico(normalizado))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.PontoResolucaoInvalido,
                "Ponto de resolução do fato fora do conjunto canônico de fases."));
        }

        return Result<string>.Success(normalizado);
    }

    internal static Result<string> ValidarBinding(string binding, OrigemFato origem, string codigo)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingObrigatorio,
                "Binding do fato é obrigatório."));
        }

        string normalizado = binding.Trim();
        if (normalizado.Length > BindingMaxLength)
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingFormatoInvalido,
                $"Binding do fato deve ter no máximo {BindingMaxLength} caracteres."));
        }

        int separador = normalizado.IndexOf(':', StringComparison.Ordinal);
        if (separador <= 0 || separador == normalizado.Length - 1)
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingFormatoInvalido,
                "Binding deve seguir o formato \"{PREFIXO}:{REFERENCIA}\", com prefixo e referência não vazios."));
        }

        string prefixo = normalizado[..separador];
        IReadOnlyList<string> prefixosAceitos = PrefixosBindingAceitos(origem);
        if (!prefixosAceitos.Contains(prefixo, StringComparer.Ordinal))
        {
            string esperado = prefixosAceitos.Count == 1
                ? $"\"{prefixosAceitos[0]}\""
                : $"um de {string.Join(", ", prefixosAceitos.Select(p => $"\"{p}\""))}";
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem,
                $"O prefixo do binding deve ser {esperado} para a origem {origem} (recebido \"{prefixo}\")."));
        }

        // REGRA_DERIVACAO referencia a regra de derivação do PRÓPRIO fato (ADR-0116): a referência
        // após o prefixo tem de ser o código do fato. Sem isso, um fato poderia apontar para a regra
        // de outro — e o metadado congelado descreveria uma derivação que não é a sua.
        if (string.Equals(prefixo, PrefixoBindingDerivadoRegra, StringComparison.Ordinal))
        {
            string referencia = normalizado[(separador + 1)..];
            if (!string.Equals(referencia, codigo, StringComparison.Ordinal))
            {
                return Result<string>.Failure(new DomainError(
                    FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente,
                    $"O binding \"{PrefixoBindingDerivadoRegra}:\" referencia a regra de derivação do próprio fato — "
                    + $"a referência deve ser \"{codigo}\" (recebido \"{referencia}\")."));
            }
        }

        return Result<string>.Success(normalizado);
    }

    private static IReadOnlyList<string> PrefixosBindingAceitos(OrigemFato origem) =>
        PrefixosBindingPorOrigem.TryGetValue(origem, out IReadOnlyList<string>? prefixos)
            ? prefixos
            : throw new ArgumentOutOfRangeException(nameof(origem), origem, "Origem de fato fora do domínio fechado.");
}
