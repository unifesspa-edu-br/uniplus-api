namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um fato do candidato no catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0143, ADR-0136):
/// o que o sistema sabe perguntar ou derivar sobre um candidato. Para cada fato, o seu
/// <see cref="Dominio"/>, a sua <see cref="Origem"/>, a sua <see cref="Cardinalidade"/>, a
/// <see cref="FonteValores"/> do categórico, o <see cref="PontoResolucao"/>, o
/// <see cref="Binding"/>, o <see cref="Escopo"/> e a proteção de dados que o tratamento exige.
/// Não armazena o valor de nenhum candidato.
/// </summary>
/// <remarks>
/// <para>
/// Dois regimes no mesmo cadastro: o fato de <b>sistema</b> é semeado por código e só tem nome e
/// descrição editáveis, porque o sistema depende da sua forma; o fato do <b>administrador</b> é
/// cadastrado em tempo de execução. Em nenhum dos dois o fato é apagado: desativar bloqueia só
/// vínculos novos, e o processo que já o usa continua com a cópia que congelou (ADR-0061).
/// </para>
/// </remarks>
public sealed class FatoCandidato : EntityBase, IAuditableEntity
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int BindingMaxLength = 200;
    private const int FinalidadeTratamentoMaxLength = 500;

    private const string PrefixoBindingDerivadoAtributo = "ATRIBUTO_CANDIDATO";
    private const string PrefixoBindingDerivadoRegra = "REGRA_DERIVACAO";
    private const string PrefixoBindingDeclarado = "CAMPO_INSCRICAO";
    private const string PrefixoBindingIntegracao = "INTEGRACAO";

    // Um fato derivado tem dois mecanismos de produção de valor: computar de um atributo do
    // candidato (FAIXA_ETARIA, RENDA_PER_CAPITA) ou referenciar a regra de derivação congelada do
    // processo (MODALIDADE). O catálogo é global e diz o mecanismo; a config do edital diz o
    // conteúdo. Declarado e Integracao seguem com um prefixo cada (ADR-0116, emenda de 2026-07-22).
    private static readonly Dictionary<OrigemFato, IReadOnlyList<string>> PrefixosBindingPorOrigem =
        new()
        {
            [OrigemFato.Derivado] = [PrefixoBindingDerivadoAtributo, PrefixoBindingDerivadoRegra],
            [OrigemFato.Declarado] = [PrefixoBindingDeclarado],
            [OrigemFato.Integracao] = [PrefixoBindingIntegracao],
        };

    public string Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public DominioFato Dominio { get; private set; }
    public OrigemFato Origem { get; private set; }
    public CardinalidadeFato Cardinalidade { get; private set; }

    /// <summary>
    /// De onde vêm os valores de um fato categórico (ADR-0136) — <see langword="null"/> para
    /// booleano e numérico. Quem consome o fato decide por ela, nunca pelo código do fato.
    /// </summary>
    public FonteValoresFato? FonteValores { get; private set; }

    /// <summary>
    /// Código canônico da fase (<see cref="FaseCanonicaCatalogo"/>) em que o valor
    /// deste fato fica conhecido (ADR-0116). Não é FK — é referência por valor,
    /// como <see cref="Codigo"/> de <c>Modalidade</c>.
    /// </summary>
    public string PontoResolucao { get; private set; } = null!;

    /// <summary>
    /// Referência de onde/como o valor do fato é produzido (ADR-0116), no formato
    /// <c>"{PREFIXO}:{REFERENCIA}"</c> com prefixo coerente com <see cref="Origem"/>.
    /// </summary>
    public string Binding { get; private set; } = null!;

    /// <summary>O formato do texto — só no fato de domínio texto.</summary>
    public FormatoTexto? Formato { get; private set; }

    /// <summary>Sobre quem o fato é respondido: o candidato ou cada membro de um grupo repetível.</summary>
    public EscopoFato Escopo { get; private set; }

    /// <summary>Classificação de proteção de dados na escala da ADR-0081.</summary>
    public ClassificacaoProtecaoDado ClassificacaoProtecao { get; private set; }

    /// <summary>Para que o dado é tratado.</summary>
    public string FinalidadeTratamento { get; private set; } = null!;

    /// <summary>Hipótese legal de tratamento, do art. 7º ou do art. 11 da LGPD conforme a classificação.</summary>
    public HipoteseLegalTratamento HipoteseLegal { get; private set; }

    /// <summary>Fato semeado pelo sistema: só nome e descrição são editáveis.</summary>
    public bool Sistema { get; private set; }

    /// <summary>Fato inativo não recebe vínculo novo; os existentes continuam.</summary>
    public bool Ativo { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private readonly List<FatoValorDominio> _valoresDominioDeclarados = [];

    /// <summary>
    /// Os valores de um categórico de fonte global, com a descrição que orienta a escolha do
    /// candidato quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/> (ADR-0116).
    /// Não ordenada aqui (mesmo padrão de <c>OfertaAtendimentoEspecializado.Condicoes</c>)
    /// — a ordenação por <c>Ordem</c>/<c>Codigo</c> é responsabilidade de quem
    /// projeta a leitura (<c>FatoCandidatoReader</c>), não do agregado.
    /// </summary>
    public IReadOnlyCollection<FatoValorDominio> ValoresDominioDeclarados => _valoresDominioDeclarados.AsReadOnly();

    // Construtor de materialização do EF Core.
    private FatoCandidato()
    {
    }

    private FatoCandidato(
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
        ProtecaoValidada protecao,
        bool sistema)
    {
        Formato = formato;
        Codigo = codigo;
        FonteValores = fonteValores;
        Nome = nome;
        Descricao = descricao;
        Dominio = dominio;
        Origem = origem;
        Cardinalidade = cardinalidade;
        PontoResolucao = pontoResolucao;
        Binding = binding;
        Escopo = escopo;
        ClassificacaoProtecao = protecao.Classificacao;
        FinalidadeTratamento = protecao.Finalidade;
        HipoteseLegal = protecao.Hipotese;
        Sistema = sistema;
        Ativo = true;
    }

    /// <summary>
    /// Factory canônica: valida o código, o nome, o domínio, a origem, a cardinalidade, a fonte
    /// dos valores, o ponto de resolução, o binding, o escopo e a proteção de dados.
    /// </summary>
    public static Result<FatoCandidato> Criar(
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
        ClassificacaoProtecaoDado classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        bool sistema)
    {
        Result<CodigoFatoCandidato> codigoResult = CodigoFatoCandidato.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(codigoResult.Error!);
        }

        Result<(string Nome, string? Descricao)> descritivo = ValidarDescritivo(nome, descricao);
        if (descritivo.IsFailure)
        {
            return Result<FatoCandidato>.Failure(descritivo.Error!);
        }

        if (dominio == DominioFato.Nenhum)
        {
            return Falha(FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório.");
        }

        if (!Enum.IsDefined(dominio))
        {
            return Falha(FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado.");
        }

        if (origem == OrigemFato.Nenhuma)
        {
            return Falha(FatoCandidatoErrorCodes.OrigemObrigatoria, "Origem do fato é obrigatória.");
        }

        if (!Enum.IsDefined(origem))
        {
            return Falha(FatoCandidatoErrorCodes.OrigemInvalida, "Origem do fato fora do vocabulário fechado.");
        }

        if (cardinalidade == CardinalidadeFato.Nenhuma)
        {
            return Falha(FatoCandidatoErrorCodes.CardinalidadeObrigatoria, "Cardinalidade do fato é obrigatória.");
        }

        if (!Enum.IsDefined(cardinalidade))
        {
            return Falha(FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado.");
        }

        bool ehCategorico = dominio == DominioFato.Categorico;
        if (ehCategorico && (fonteValores is null or FonteValoresFato.Nenhuma || !Enum.IsDefined(fonteValores.Value)))
        {
            return Falha(
                FatoCandidatoErrorCodes.FonteValoresObrigatoria,
                "Fato categórico precisa declarar a fonte dos seus valores.");
        }

        if (!ehCategorico && fonteValores is not null)
        {
            return Falha(
                FatoCandidatoErrorCodes.FonteValoresForaDeCategorico,
                "Só fato categórico declara a fonte dos seus valores.");
        }

        bool ehTexto = dominio == DominioFato.Texto;
        if (ehTexto && (formato is null or FormatoTexto.Nenhum || !Enum.IsDefined(formato.Value)))
        {
            return Falha(FatoCandidatoErrorCodes.FormatoObrigatorio, "Fato de domínio texto precisa declarar o formato.");
        }

        if (!ehTexto && formato is not null)
        {
            return Falha(FatoCandidatoErrorCodes.FormatoForaDeTexto, "Só fato de domínio texto declara formato.");
        }

        Result<string> pontoResolucaoResult = ValidarPontoResolucao(pontoResolucao);
        if (pontoResolucaoResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(pontoResolucaoResult.Error!);
        }

        Result<string> bindingResult = ValidarBinding(binding, origem, codigoResult.Value!.Valor);
        if (bindingResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(bindingResult.Error!);
        }

        // O fato do administrador não tem código que calcule ou traga o valor: é declarado, ou
        // derivado pela regra que ele mesmo cadastra. Atributo do candidato e integração só existem
        // em fato de sistema.
        if (!sistema && (origem == OrigemFato.Integracao || bindingResult.Value!.StartsWith(PrefixoBindingDerivadoAtributo + ":", StringComparison.Ordinal)))
        {
            return Falha(
                FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema,
                "Fato do administrador é declarado ou derivado por regra; atributo do candidato e integração só existem em fato de sistema.");
        }

        if (escopo == EscopoFato.Nenhum || !Enum.IsDefined(escopo))
        {
            return Falha(FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato é obrigatório.");
        }

        Result<ProtecaoValidada> protecao = ValidarProtecao(dominio, classificacaoProtecao, finalidadeTratamento, hipoteseLegal);
        if (protecao.IsFailure)
        {
            return Result<FatoCandidato>.Failure(protecao.Error!);
        }

        return Result<FatoCandidato>.Success(new FatoCandidato(
            codigoResult.Value!.Valor,
            descritivo.Value.Nome,
            descritivo.Value.Descricao,
            dominio,
            origem,
            cardinalidade,
            fonteValores,
            formato,
            pontoResolucaoResult.Value!,
            bindingResult.Value!,
            escopo,
            protecao.Value,
            sistema));
    }

    /// <summary>
    /// Altera o nome e a descrição. Os eixos do fato — domínio, origem, cardinalidade, escopo,
    /// fonte, ponto de resolução, vínculo e proteção de dados — não se editam depois do cadastro:
    /// mudança num eixo é um fato novo (ADR-0136).
    /// </summary>
    public Result AlterarDescritivo(string nome, string? descricao)
    {
        Result<(string Nome, string? Descricao)> descritivo = ValidarDescritivo(nome, descricao);
        if (descritivo.IsFailure)
        {
            return Result.Failure(descritivo.Error!);
        }

        (Nome, Descricao) = descritivo.Value;
        return Result.Success();
    }

    /// <summary>Reativa o fato do administrador, que volta a aceitar vínculos novos.</summary>
    public Result Ativar()
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (Ativo)
        {
            return Result.Failure(new DomainError(FatoCandidatoErrorCodes.JaAtivo, "O fato já está ativo."));
        }

        Ativo = true;
        return Result.Success();
    }

    /// <summary>
    /// Desativa o fato do administrador: vínculos novos são recusados, os existentes continuam. O
    /// fato de sistema não é desativado.
    /// </summary>
    public Result Desativar()
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (!Ativo)
        {
            return Result.Failure(new DomainError(FatoCandidatoErrorCodes.JaDesativado, "O fato já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    /// <summary>
    /// Adiciona um valor ao conjunto fechado de um categórico estático (ADR-0116).
    /// Só o agregado conhece o necessário para validar: que o próprio
    /// <see cref="Dominio"/> é <see cref="DominioFato.Categorico"/>, que o
    /// <paramref name="codigo"/> (normalizado por trim, comparação ordinal) não
    /// colide com um irmão já adicionado, e que a <paramref name="descricao"/> é
    /// obrigatória quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>.
    /// </summary>
    public Result AdicionarValorDominio(string codigo, string? descricao, int ordem, bool ativo)
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (Dominio != DominioFato.Categorico)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico,
                "Valores de domínio só podem ser adicionados a um fato categórico."));
        }

        if (FonteValores != FonteValoresFato.Global)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal,
                "Valores de domínio só são declarados no catálogo quando a fonte dos valores é global; "
                + "nas demais fontes, os valores vêm do processo."));
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoObrigatorio,
                "Código do valor de domínio é obrigatório."));
        }

        string codigoNormalizado = codigo.Trim();
        if (codigoNormalizado.Length > FatoValorDominio.CodigoMaxLength)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoTamanho,
                $"Código do valor de domínio deve ter no máximo {FatoValorDominio.CodigoMaxLength} caracteres."));
        }

        if (_valoresDominioDeclarados.Any(v => string.Equals(v.Codigo, codigoNormalizado, StringComparison.Ordinal)))
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoDuplicado,
                $"Já existe um valor de domínio com o código '{codigoNormalizado}' neste fato."));
        }

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > FatoValorDominio.DescricaoMaxLength })
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.DescricaoTamanho,
                $"Descrição do valor de domínio deve ter no máximo {FatoValorDominio.DescricaoMaxLength} caracteres."));
        }

        if (Origem == OrigemFato.Declarado && descricaoNormalizada is null)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.DescricaoObrigatoria,
                "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO."));
        }

        if (ordem < 0)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.OrdemInvalida,
                "Ordem do valor de domínio não pode ser negativa."));
        }

        _valoresDominioDeclarados.Add(FatoValorDominio.Criar(Id, codigoNormalizado, descricaoNormalizada, ordem, ativo));
        return Result.Success();
    }

    private DomainError? RecusaSeSistema() =>
        Sistema
            ? new DomainError(
                FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao,
                "Fato de sistema só tem o nome e a descrição editáveis.")
            : null;

    private static Result<(string Nome, string? Descricao)> ValidarDescritivo(string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<(string, string?)>.Failure(new DomainError(
                FatoCandidatoErrorCodes.NomeObrigatorio, "Nome do fato é obrigatório."));
        }

        string nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length > NomeMaxLength)
        {
            return Result<(string, string?)>.Failure(new DomainError(
                FatoCandidatoErrorCodes.NomeTamanho, $"Nome do fato deve ter no máximo {NomeMaxLength} caracteres."));
        }

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > DescricaoMaxLength })
        {
            return Result<(string, string?)>.Failure(new DomainError(
                FatoCandidatoErrorCodes.DescricaoTamanho, $"Descrição do fato deve ter no máximo {DescricaoMaxLength} caracteres."));
        }

        return Result<(string, string?)>.Success((nomeNormalizado, descricaoNormalizada));
    }

    private static Result<ProtecaoValidada> ValidarProtecao(
        DominioFato dominio, ClassificacaoProtecaoDado classificacao, string finalidade, HipoteseLegalTratamento hipotese)
    {
        if (classificacao == ClassificacaoProtecaoDado.Nenhuma || !Enum.IsDefined(classificacao))
        {
            return FalhaProtecao(
                FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria, "Classificação de proteção de dados do fato é obrigatória.");
        }

        // Texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), data e
        // endereço identificam ou localizam a pessoa: nunca são menos que dado pessoal.
        if (dominio is DominioFato.Texto or DominioFato.Data or DominioFato.Endereco
            && classificacao is not (ClassificacaoProtecaoDado.Pessoal or ClassificacaoProtecaoDado.Sensivel))
        {
            return FalhaProtecao(
                FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio,
                "Fato de texto, data ou endereço é classificado como pessoal ou sensível.");
        }

        if (string.IsNullOrWhiteSpace(finalidade))
        {
            return FalhaProtecao(
                FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria, "Finalidade do tratamento do fato é obrigatória.");
        }

        string finalidadeNormalizada = finalidade.Trim();
        if (finalidadeNormalizada.Length > FinalidadeTratamentoMaxLength)
        {
            return FalhaProtecao(
                FatoCandidatoErrorCodes.FinalidadeTratamentoTamanho,
                $"Finalidade do tratamento deve ter no máximo {FinalidadeTratamentoMaxLength} caracteres.");
        }

        if (hipotese == HipoteseLegalTratamento.Nenhuma || !Enum.IsDefined(hipotese))
        {
            return FalhaProtecao(FatoCandidatoErrorCodes.HipoteseLegalObrigatoria, "Hipótese legal de tratamento do fato é obrigatória.");
        }

        if (!HipotesesLegaisTratamento.AdmiteClassificacao(hipotese, classificacao))
        {
            return FalhaProtecao(
                FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao,
                "A hipótese legal não cabe na classificação: dado sensível usa as hipóteses do art. 11 da LGPD, e os demais as do art. 7º.");
        }

        return Result<ProtecaoValidada>.Success(new ProtecaoValidada(classificacao, finalidadeNormalizada, hipotese));
    }

    private static Result<ProtecaoValidada> FalhaProtecao(string codigo, string mensagem) =>
        Result<ProtecaoValidada>.Failure(new DomainError(codigo, mensagem));

    private static Result<string> ValidarPontoResolucao(string pontoResolucao)
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

    private static Result<string> ValidarBinding(string binding, OrigemFato origem, string codigo)
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

    private static Result<FatoCandidato> Falha(string codigo, string mensagem) =>
        Result<FatoCandidato>.Failure(new DomainError(codigo, mensagem));

    /// <summary>A proteção de dados já validada, aplicada na criação.</summary>
    private readonly record struct ProtecaoValidada(
        ClassificacaoProtecaoDado Classificacao, string Finalidade, HipoteseLegalTratamento Hipotese);
}
