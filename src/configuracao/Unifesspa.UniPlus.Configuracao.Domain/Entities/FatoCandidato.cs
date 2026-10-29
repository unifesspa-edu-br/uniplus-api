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

    /// <summary>
    /// O nome social preferido pelo titular: o único texto público, porque é a identificação que
    /// ele escolhe para aparecer (ADR-0082, ADR-0136).
    /// </summary>
    public const string CodigoDoNomeSocial = "NOME_SOCIAL";

    private const string PrefixoBindingDerivadoAtributo = VinculoDeFato.AtributoDoCandidato;
    private const string PrefixoBindingDerivadoRegra = VinculoDeFato.RegraDeDerivacao;
    private const string PrefixoBindingDerivadoClassificacao = VinculoDeFato.Classificacao;
    private const string PrefixoBindingDeclarado = VinculoDeFato.CampoDoFormulario;
    private const string PrefixoBindingIntegracao = VinculoDeFato.Integracao;
    private const string PrefixoBindingAgregacao = VinculoDeFato.AgregacaoDeGrupo;

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
    public ClassificacaoDeProtecao ClassificacaoProtecao { get; private set; }

    /// <summary>Para que o dado é tratado.</summary>
    public string FinalidadeTratamento { get; private set; } = null!;

    /// <summary>Hipótese legal de tratamento, do art. 7º ou do art. 11 da LGPD conforme a classificação.</summary>
    public HipoteseLegalTratamento HipoteseLegal { get; private set; }

    /// <summary>Fato semeado pelo sistema: só nome e descrição são editáveis.</summary>
    public bool Sistema { get; private set; }

    /// <summary>Fato inativo não recebe vínculo novo; os existentes continuam.</summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// As regras padrão do derivado por regra do administrador (ADR-0136): <c>{quando, contribui}</c>,
    /// sem ordem, avaliadas por união. São o ponto de partida que o processo copia; a cópia do
    /// processo é a que vale.
    /// </summary>
    public IReadOnlyList<RegraDerivacao> RegrasPadrao { get; private set; } = [];

    /// <summary>
    /// Os fatos de que o derivado do sistema depende (<see cref="DerivadosDoSistema"/>), registrados
    /// pelo seed; vazio nos demais fatos.
    /// </summary>
    public IReadOnlyList<string> Dependencias { get; private set; } = [];

    /// <summary>
    /// O fato de membro que o agregado aponta (ADR-0138), ou <see langword="null"/> quando o fato não
    /// é agregado sobre grupo repetível.
    /// </summary>
    public string? FatoDeMembroAgregado => VinculoDeFato.Nomeado(Binding, PrefixoBindingAgregacao);

    /// <summary>O valor do fato vem da regra de derivação do próprio fato.</summary>
    public bool DerivadoPorRegra =>
        Binding.StartsWith(PrefixoBindingDerivadoRegra + ":", StringComparison.Ordinal);

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
        ProtecaoDeDados protecao,
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
    /// dos valores, o formato, o ponto de resolução, o vínculo, o escopo e a proteção de dados. As
    /// violações independentes são acumuladas (ADR-0125); a que depende de outro campo só é
    /// conferida quando esse campo é válido.
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
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        bool sistema)
    {
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        Result<CodigoFatoCandidato> codigoResult = CodigoFatoCandidato.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }

        Result<(string Nome, string? Descricao)> descritivo = ValidarDescritivo(nome, descricao);
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
            dominio, classificacaoProtecao, finalidadeTratamento, hipoteseLegal,
            nomeSocialPublico: sistema
                && string.Equals(codigo?.Trim(), CodigoDoNomeSocial, StringComparison.Ordinal)
                && dominio == DominioFato.Texto
                && classificacaoProtecao == ClassificacaoDeProtecao.Publico);
        erros.AddRange(protecao.Errors);

        if (erros.Count > 0)
        {
            return Result<FatoCandidato>.ValidationFailure(erros);
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
            bindingValidado!,
            escopo,
            protecao.Value,
            sistema));
    }

    /// <summary>
    /// O domínio, a cardinalidade, a fonte dos valores e o formato, que o cadastro e a alteração da
    /// definição conferem do mesmo jeito (ADR-0125): as violações acumulam, e a que depende do
    /// domínio só é conferida com o domínio válido.
    /// </summary>
    private static void ConferirEixosDeValor(
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

    private static void ConferirEscopo(List<FieldError> erros, EscopoFato escopo)
    {
        if (escopo == EscopoFato.Nenhum || !Enum.IsDefined(escopo))
        {
            erros.Add(new("escopo", new DomainError(FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato é obrigatório.")));
        }
    }

    /// <summary>
    /// Cadastro de fato pelo administrador (ADR-0136): sempre declarado, com o vínculo ao campo de
    /// formulário gerado a partir do código, e nunca de sistema.
    /// </summary>
    public static Result<FatoCandidato> CriarDoAdministrador(
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
        HipoteseLegalTratamento hipoteseLegal) =>
        Criar(
            codigo, nome, descricao, dominio, OrigemFato.Declarado, cardinalidade, fonteValores, formato, pontoResolucao,
            $"{PrefixoBindingDeclarado}:{codigo?.Trim()}", escopo, classificacaoProtecao, finalidadeTratamento, hipoteseLegal,
            sistema: false);

    /// <summary>
    /// Cadastro de derivado por regra pelo administrador (ADR-0136): booleano, verdadeiro quando
    /// alguma regra ativa, ou categórico de valores próprios, com a união das contribuições. As
    /// regras padrão vêm depois, por <see cref="DefinirRegrasPadrao"/>, porque citam os valores que
    /// o categórico ainda vai receber.
    /// </summary>
    public static Result<FatoCandidato> CriarDerivadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        string pontoResolucao,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal)
    {
        (CardinalidadeFato cardinalidade, FonteValoresFato? fonteValores) = EixosInferidosDoDerivado(dominio);
        Result<FatoCandidato> criado = Criar(
            codigo, nome, descricao, dominio, OrigemFato.Derivado,
            cardinalidade,
            fonteValores,
            formato: null, pontoResolucao, $"{PrefixoBindingDerivadoRegra}:{codigo?.Trim()}", escopo,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false);
        List<FieldError> recusasDoDominio = [];
        if (ConferirDominioDoDerivado(recusasDoDominio, dominio))
        {
            return criado;
        }

        // Com o domínio recusado, o que depende dele (formato, fonte) não é conferido.
        return Result<FatoCandidato>.ValidationFailure(
            [.. recusasDoDominio, .. criado.Errors.Where(static e => e.Field is not ("dominio" or "formato" or "fonteValores"))]);
    }

    /// <summary>
    /// Cadastro do agregado sobre grupo repetível pelo administrador (ADR-0138, UNI-REQ-0146): o
    /// vínculo <c>AGREGACAO_GRUPO:</c> aponta o fato de membro, que precisa existir, estar ativo e ser
    /// fato declarado de membro de grupo de domínio que agrega. O domínio, a cardinalidade e a fonte
    /// dos valores saem do fato de membro — booleano dá "existe membro que…", categórico dá "valores
    /// presentes" —, e o agregado não tem valores próprios. Não protege menos nem resolve antes do
    /// fato de membro. As recusas se acumulam (ADR-0125); sem fato de membro válido, as que
    /// dependem dele não são conferidas.
    /// </summary>
    public static Result<FatoCandidato> CriarAgregadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        string fatoDeMembroCodigo,
        CatalogoDeFatos catalogo,
        string pontoResolucao,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal)
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
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false);

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
    private static (CardinalidadeFato Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoDerivado(DominioFato dominio) =>
        dominio == DominioFato.Categorico
            ? (CardinalidadeFato.Multivalorado, FonteValoresFato.Global)
            : (CardinalidadeFato.Escalar, null);

    /// <summary>
    /// O domínio, a cardinalidade e a fonte que o agregado infere da operação sobre o fato de membro:
    /// valores presentes dá categórico multivalorado com a fonte do membro, e o restante dá booleano.
    /// </summary>
    private static (DominioFato Dominio, CardinalidadeFato Cardinalidade, FonteValoresFato? FonteValores) EixosInferidosDoAgregado(
        OperacaoAgregado operacao, FonteValoresFato? fonteDoMembro) =>
        operacao == OperacaoAgregado.ValoresPresentes
            ? (DominioFato.Categorico, CardinalidadeFato.Multivalorado, fonteDoMembro)
            : (DominioFato.Booleano, CardinalidadeFato.Escalar, null);

    /// <summary>
    /// O agregado não protege menos nem resolve antes do fato de membro: a recusa vai no campo do
    /// eixo, e só quando o eixo informado é válido.
    /// </summary>
    private static void ConferirContraOMembro(
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

    /// <summary>
    /// Substitui as regras padrão do derivado por regra do administrador, conferidas contra o
    /// catálogo (<see cref="ValidadorRegrasPadrao"/>). Lista vazia remove as regras.
    /// </summary>
    public Result DefinirRegrasPadrao(IReadOnlyList<RegraDerivacao> regras, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (!DerivadoPorRegra)
        {
            return Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.RegrasPadraoSoEmDerivadoPorRegra,
                "Só o fato derivado por regra tem regras padrão."));
        }

        Result validacao = ValidadorRegrasPadrao.Validar(this, regras, catalogo);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        RegrasPadrao = [.. regras];
        return Result.Success();
    }

    /// <summary>
    /// Desativa um valor do fato do administrador: condição nova que o cite é recusada, e a que já o
    /// citava continua. O valor do fato de sistema só muda por nova versão do sistema.
    /// </summary>
    public Result DesativarValor(string codigo) => DefinirAtivoDoValor(codigo, ativo: false);

    /// <summary>Reativa um valor do fato do administrador.</summary>
    public Result ReativarValor(string codigo) => DefinirAtivoDoValor(codigo, ativo: true);

    private Result DefinirAtivoDoValor(string codigo, bool ativo)
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (ValorPorCodigo(codigo) is not { } valor)
        {
            return Result.Failure(ValorNaoEncontrado);
        }

        if (valor.Ativo == ativo)
        {
            return Result.Failure(ativo
                ? new DomainError(FatoValorDominioErrorCodes.JaAtivo, "O valor já está ativo.")
                : new DomainError(FatoValorDominioErrorCodes.JaDesativado, "O valor já está desativado."));
        }

        valor.DefinirAtivo(ativo);
        return Result.Success();
    }

    /// <summary>
    /// Altera o nome e a descrição. Os eixos têm a alteração própria, <see cref="AlterarDefinicao"/>.
    /// </summary>
    public Result AlterarDescritivo(string nome, string? descricao)
    {
        Result<(string Nome, string? Descricao)> descritivo = ValidarDescritivo(nome, descricao);
        if (descritivo.IsFailure)
        {
            return Result.ValidationFailure(descritivo.Errors);
        }

        (Nome, Descricao) = descritivo.Value;
        return Result.Success();
    }

    /// <summary>
    /// Altera os eixos do fato do administrador — domínio, cardinalidade, fonte dos valores, formato,
    /// escopo, ponto de resolução e proteção de dados — no subconjunto que o cadastro do tipo dele
    /// aceita: o declarado, todos; o derivado por regra, domínio (booleano ou categórico), escopo,
    /// ponto de resolução e proteção; o agregado, só ponto de resolução e proteção. Código, origem e
    /// vínculo não mudam (ADR-0136). As validações são as do cadastro (ADR-0125), mais a coerência do
    /// fato com os valores e as dependências que já tem. O fato não muda quando há recusa.
    /// </summary>
    public Result AlterarDefinicao(DefinicaoDoFato definicao, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        bool agregado = FatoDeMembroAgregado is not null;
        bool derivado = DerivadoPorRegra;
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));
        void RecusarSeInformado(string campo, bool informado)
        {
            if (informado)
            {
                Recusar(campo, FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato,
                    agregado
                        ? "O agregado infere este eixo do fato de membro; só o ponto de resolução e a proteção de dados se alteram."
                        : "O fato derivado por regra infere este eixo do domínio; não o informe.");
            }
        }

        DominioFato dominio = Dominio;
        CardinalidadeFato cardinalidade = Cardinalidade;
        FonteValoresFato? fonteValores = FonteValores;
        FormatoTexto? formato = Formato;
        EscopoFato escopo = Escopo;
        bool eixosDeValorValidos = true;
        // Só o domínio aplicável confere a proteção mínima: o recusado (fora do vocabulário, ou texto
        // num derivado) nunca será gravado, e a recusa dele já foi dada.
        bool dominioAplicavel = true;

        if (agregado)
        {
            RecusarSeInformado("dominio", definicao.Dominio is not null);
            RecusarSeInformado("cardinalidade", definicao.Cardinalidade is not null);
            RecusarSeInformado("fonteValores", definicao.FonteValores is not null);
            RecusarSeInformado("formato", definicao.Formato is not null);
            RecusarSeInformado("escopo", definicao.Escopo is not null);
        }
        else
        {
            dominio = definicao.Dominio ?? DominioFato.Nenhum;
            escopo = definicao.Escopo ?? EscopoFato.Nenhum;
            ConferirEscopo(erros, escopo);
            if (derivado)
            {
                RecusarSeInformado("cardinalidade", definicao.Cardinalidade is not null);
                RecusarSeInformado("fonteValores", definicao.FonteValores is not null);
                RecusarSeInformado("formato", definicao.Formato is not null);
                (cardinalidade, fonteValores) = EixosInferidosDoDerivado(dominio);
                formato = null;
                if (!ConferirDominioDoDerivado(erros, dominio))
                {
                    eixosDeValorValidos = false;
                    dominioAplicavel = false;
                }
            }
            else
            {
                cardinalidade = definicao.Cardinalidade ?? CardinalidadeFato.Nenhuma;
                fonteValores = definicao.FonteValores;
                formato = definicao.Formato;
                int antes = erros.Count;
                ConferirEixosDeValor(erros, dominio, cardinalidade, fonteValores, formato);
                eixosDeValorValidos = erros.Count == antes;
                dominioAplicavel = dominio != DominioFato.Nenhum && Enum.IsDefined(dominio);
            }

            if (eixosDeValorValidos && _valoresDominioDeclarados.Count > 0)
            {
                // Os valores do catálogo só existem no categórico de fonte global: o eixo novo que
                // não os comporta é recusado, e o valor não é descartado em silêncio.
                if (dominio != DominioFato.Categorico)
                {
                    eixosDeValorValidos = false;
                    Recusar("dominio", FatoCandidatoErrorCodes.DominioNaoComportaOsValores,
                        "O fato já tem valores cadastrados e só o categórico os tem; desativar os valores não basta, porque o código de um valor nunca é removido.");
                }
                else if (fonteValores != FonteValoresFato.Global)
                {
                    eixosDeValorValidos = false;
                    Recusar("fonteValores", FatoCandidatoErrorCodes.FonteNaoComportaOsValores,
                        "O fato já tem valores cadastrados no catálogo e só a fonte global os tem.");
                }
            }
        }

        Result<string> ponto = ValidarPontoResolucao(definicao.PontoResolucao);
        if (ponto.IsFailure)
        {
            erros.Add(new("pontoResolucao", ponto.Error!));
        }

        Result<ProtecaoDeDados> protecao = ValidarProtecao(
            dominioAplicavel ? dominio : null, definicao.ClassificacaoProtecao, definicao.FinalidadeTratamento, definicao.HipoteseLegal,
            nomeSocialPublico: false);
        erros.AddRange(protecao.Errors);

        // A coerência com as dependências do próprio fato é conferida com o que há de válido na
        // entrada; o eixo recusado acima fica como está, e a recusa dele já foi dada.
        EixosDaDefinicao anteriores = new(
            Dominio, Cardinalidade, FonteValores, Formato, PontoResolucao, Escopo,
            ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);
        EixosDaDefinicao propostos = new(
            eixosDeValorValidos ? dominio : Dominio,
            eixosDeValorValidos ? cardinalidade : Cardinalidade,
            eixosDeValorValidos ? fonteValores : FonteValores,
            eixosDeValorValidos ? formato : Formato,
            ponto.IsSuccess ? ponto.Value! : PontoResolucao,
            erros.Any(static e => e.Field == "escopo") ? null : escopo,
            protecao.IsSuccess ? protecao.Value.Classificacao : ClassificacaoProtecao,
            protecao.IsSuccess ? protecao.Value.Finalidade : FinalidadeTratamento,
            protecao.IsSuccess ? protecao.Value.Hipotese : HipoteseLegal);
        // Só a recusa que a alteração causa conta: a que já existia antes dela não a trava.
        List<FieldError> dependenciasAntes = [];
        ConferirComAsDependencias(dependenciasAntes, catalogo, agregado, derivado, true, true);
        Aplicar(propostos);
        List<FieldError> dependenciasDepois = [];
        ConferirComAsDependencias(dependenciasDepois, catalogo, agregado, derivado, ponto.IsSuccess, protecao.IsSuccess);
        erros.AddRange(DiferencaDeRecusas.Novas(dependenciasDepois, dependenciasAntes));

        if (erros.Count > 0)
        {
            Aplicar(anteriores);
            return Result.ValidationFailure(erros);
        }

        return Result.Success();
    }

    /// <summary>
    /// O que o agregado deixa de ser quando o fato de membro passa a ser <paramref name="membro"/>:
    /// o domínio, a cardinalidade e a fonte que ele gravou são os inferidos do membro, e ele não
    /// protege menos nem resolve antes dele. Vazio quando o agregado continua valendo.
    /// </summary>
    public IReadOnlyList<FieldError> MotivosPelosQuaisDeixaDeAgregar(FatoCandidato membro, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(membro);
        ArgumentNullException.ThrowIfNull(catalogo);

        List<FieldError> motivos = [];
        static FieldError Motivo(string motivo, string mensagem) =>
            new($"agregado.{motivo}", new DomainError(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato, mensagem));
        OperacaoAgregado operacao = AgregadoDeGrupo.OperacaoDoDominio(DominiosFato.ParaTokenCanonico(membro.Dominio));
        if (membro is not { Escopo: EscopoFato.MembroGrupo, Origem: OrigemFato.Declarado })
        {
            motivos.Add(Motivo("membro", $"'{membro.Codigo}' deixa de ser fato declarado de membro de grupo, e o agregado resume o que cada membro respondeu."));
            return motivos;
        }

        if (operacao == OperacaoAgregado.Nenhuma)
        {
            motivos.Add(Motivo("dominioSemResumo", $"'{membro.Codigo}' passa a um domínio que o agregado não resume (só booleano e categórico)."));
            return motivos;
        }

        if (!membro.Ativo)
        {
            motivos.Add(Motivo("membroInativo", $"'{membro.Codigo}' está desativado no catálogo, e o agregado resume um fato de membro ativo."));
        }

        (DominioFato dominioEsperado, CardinalidadeFato cardinalidadeEsperada, FonteValoresFato? fonteEsperada) =
            EixosInferidosDoAgregado(operacao, membro.FonteValores);
        if (Dominio != dominioEsperado)
        {
            motivos.Add(Motivo("dominio", $"o domínio de '{membro.Codigo}' mudou, e o agregado gravou o domínio {DominiosFato.ParaTokenCanonico(Dominio)} inferido dele."));
        }
        else if (Cardinalidade != cardinalidadeEsperada)
        {
            motivos.Add(Motivo("cardinalidade", $"a cardinalidade do agregado não acompanha a inferida de '{membro.Codigo}': o agregado gravou {CardinalidadesFato.ParaTokenCanonico(Cardinalidade)}."));
        }
        else if (FonteValores != fonteEsperada)
        {
            motivos.Add(Motivo("fonte", $"a fonte dos valores de '{membro.Codigo}' mudou, e o agregado gravou a fonte inferida dela."));
        }

        List<FieldError> erros = [];
        ConferirContraOMembro(erros, membro, PontoResolucao, ClassificacaoProtecao, catalogo);
        motivos.AddRange(erros);
        return motivos;
    }

    private void ConferirComAsDependencias(
        List<FieldError> erros, CatalogoDeFatos catalogo, bool agregado, bool derivado, bool pontoValido, bool protecaoValida)
    {
        if (agregado)
        {
            FatoCandidato? membro = catalogo.Fatos.FirstOrDefault(f => string.Equals(f.Codigo, FatoDeMembroAgregado, StringComparison.Ordinal));
            if (membro is not null && (pontoValido || protecaoValida))
            {
                List<FieldError> contraOMembro = [];
                ConferirContraOMembro(contraOMembro, membro, PontoResolucao, ClassificacaoProtecao, catalogo);
                erros.AddRange(contraOMembro.Where(e => e.Field == "pontoResolucao" ? pontoValido : protecaoValida));
            }

            return;
        }

        if (derivado && RegrasPadrao.Count > 0)
        {
            // As regras padrão são as do fato, e a mudança de domínio, de ponto de resolução ou de
            // proteção pode deixá-las sem valer. A recusa vai no eixo que a causou, não na regra.
            Result regras = ValidadorRegrasPadrao.Validar(this, RegrasPadrao, catalogo);
            erros.AddRange(regras.Errors.Select(static e => e.Error.Code switch
            {
                FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia => e with { Field = "classificacaoProtecao" },
                FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia => e with { Field = "pontoResolucao" },
                _ => e with { Field = "dominio" },
            }).Where(e => e.Field switch
            {
                "classificacaoProtecao" => protecaoValida,
                "pontoResolucao" => pontoValido,
                _ => true,
            }));
        }
    }

    private void Aplicar(EixosDaDefinicao eixos)
    {
        Dominio = eixos.Dominio;
        Cardinalidade = eixos.Cardinalidade;
        FonteValores = eixos.FonteValores;
        Formato = eixos.Formato;
        PontoResolucao = eixos.PontoResolucao;
        Escopo = eixos.Escopo ?? Escopo;
        ClassificacaoProtecao = eixos.Classificacao;
        FinalidadeTratamento = eixos.Finalidade;
        HipoteseLegal = eixos.Hipotese;
    }

    private readonly record struct EixosDaDefinicao(
        DominioFato Dominio,
        CardinalidadeFato Cardinalidade,
        FonteValoresFato? FonteValores,
        FormatoTexto? Formato,
        string PontoResolucao,
        EscopoFato? Escopo,
        ClassificacaoDeProtecao Classificacao,
        string Finalidade,
        HipoteseLegalTratamento Hipotese);

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
    /// obrigatória quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>. A
    /// <paramref name="orientacao"/>, opcional, é o que o candidato vê abaixo da opção.
    /// </summary>
    public Result AdicionarValorDominio(string codigo, string? descricao, int ordem, bool ativo, string? orientacao = null)
    {
        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        // As recusas de estado acima saem de imediato; as do valor são independentes e acumulam,
        // cada uma no seu campo (ADR-0125).
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        string codigoNormalizado = codigo?.Trim() ?? string.Empty;
        if (codigoNormalizado.Length == 0)
        {
            Recusar("codigo", FatoValorDominioErrorCodes.CodigoObrigatorio, "Código do valor de domínio é obrigatório.");
        }
        else if (codigoNormalizado.Length > FatoValorDominio.CodigoMaxLength)
        {
            Recusar("codigo", FatoValorDominioErrorCodes.CodigoTamanho,
                $"Código do valor de domínio deve ter no máximo {FatoValorDominio.CodigoMaxLength} caracteres.");
        }
        else if (_valoresDominioDeclarados.Any(v => string.Equals(v.Codigo, codigoNormalizado, StringComparison.Ordinal)))
        {
            Recusar("codigo", FatoValorDominioErrorCodes.CodigoDuplicado,
                $"Já existe um valor de domínio com o código '{codigoNormalizado}' neste fato.");
        }

        (string? descricaoNormalizada, string? orientacaoNormalizada) = ValidarTextosDoValor(descricao, orientacao, erros);

        if (ordem < 0)
        {
            Recusar("ordem", FatoValorDominioErrorCodes.OrdemInvalida, "Ordem do valor de domínio não pode ser negativa.");
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        _valoresDominioDeclarados.Add(FatoValorDominio.Criar(Id, codigoNormalizado, descricaoNormalizada, ordem, ativo, orientacaoNormalizada));
        return Result.Success();
    }

    /// <summary>
    /// Corrige a descrição e a orientação de um valor do fato do administrador, com as mesmas regras
    /// do acréscimo. O código do valor não muda: mudar o sentido de uma opção é acrescentar outro
    /// valor e desativar o antigo.
    /// </summary>
    public Result CorrigirValor(string codigo, string? descricao, string? orientacao)
    {
        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (ValorPorCodigo(codigo) is not { } valor)
        {
            return Result.Failure(ValorNaoEncontrado);
        }

        List<FieldError> erros = [];
        (string? descricaoNormalizada, string? orientacaoNormalizada) = ValidarTextosDoValor(descricao, orientacao, erros);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        valor.CorrigirTextos(descricaoNormalizada, orientacaoNormalizada);
        return Result.Success();
    }

    /// <summary>
    /// Reordena os valores do fato do administrador pela lista completa de códigos — ativos e
    /// desativados —, na ordem em que passam a aparecer. A lista que deixa um valor de fora, repete
    /// um código ou cita um código que o fato não tem é recusada inteira, e as três faltas acumulam.
    /// </summary>
    public Result ReordenarValores(IReadOnlyList<string> codigosEmOrdem)
    {
        ArgumentNullException.ThrowIfNull(codigosEmOrdem);

        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        string[] informados = [.. codigosEmOrdem.Select(static c => c?.Trim() ?? string.Empty)];
        Dictionary<string, FatoValorDominio> porCodigo = _valoresDominioDeclarados.ToDictionary(static v => v.Codigo, StringComparer.Ordinal);
        HashSet<string> informadosSet = new(informados, StringComparer.Ordinal);
        List<FieldError> erros = [];

        if (informados.Any(static c => c.Length == 0))
        {
            erros.Add(new("codigos", new DomainError(
                FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido,
                "A ordem tem um código em branco; informe o código de cada valor do fato.")));
        }

        string[] estranhos = [.. informados.Where(c => c.Length > 0 && !porCodigo.ContainsKey(c)).Distinct(StringComparer.Ordinal)];
        if (estranhos.Length > 0)
        {
            erros.Add(new("codigos", new DomainError(
                FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido,
                $"O fato não tem os valores: {string.Join(", ", estranhos)}.")));
        }

        string[] repetidos = [.. informados.Where(static c => c.Length > 0).GroupBy(static c => c, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1).Select(static g => g.Key)];
        if (repetidos.Length > 0)
        {
            erros.Add(new("codigos", new DomainError(
                FatoValorDominioErrorCodes.OrdemComCodigoRepetido,
                $"Valores repetidos na ordem: {string.Join(", ", repetidos)}.")));
        }

        string[] ausentes = [.. porCodigo.Keys.Where(c => !informadosSet.Contains(c)).Order(StringComparer.Ordinal)];
        if (ausentes.Length > 0)
        {
            erros.Add(new("codigos", new DomainError(
                FatoValorDominioErrorCodes.OrdemSemTodosOsValores,
                $"A ordem deve trazer todos os valores do fato, ativos e desativados; faltam: {string.Join(", ", ausentes)}.")));
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        // Valor na posição em que já está não é regravado: sem mudança, o EF não marca a linha e a
        // versão do fato não avança.
        for (int posicao = 0; posicao < informados.Length; posicao++)
        {
            porCodigo[informados[posicao]].DefinirOrdem(posicao);
        }

        return Result.Success();
    }

    /// <summary>
    /// As recusas de estado comuns a tudo que mexe nos valores: só o fato do administrador, categórico,
    /// de fonte global e que não seja agregado tem valores próprios.
    /// </summary>
    private DomainError? RecusaDeEstadoParaValores()
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return recusa;
        }

        if (Dominio != DominioFato.Categorico)
        {
            return new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico,
                "Só fato categórico tem valores de domínio próprios.");
        }

        if (FatoDeMembroAgregado is { } membro)
        {
            return new DomainError(
                FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios,
                $"Os valores do agregado são os do fato de membro '{membro}'; mantenha-os nele.");
        }

        if (FonteValores != FonteValoresFato.Global)
        {
            return new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal,
                "Valores de domínio só são declarados no catálogo quando a fonte dos valores é global; "
                + "nas demais fontes, os valores vêm do processo.");
        }

        return null;
    }

    /// <summary>A descrição e a orientação normalizadas; as recusas acumulam em <paramref name="erros"/>, cada uma no seu campo (ADR-0125).</summary>
    private (string? Descricao, string? Orientacao) ValidarTextosDoValor(string? descricao, string? orientacao, List<FieldError> erros)
    {
        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > FatoValorDominio.DescricaoMaxLength })
        {
            erros.Add(new("descricao", new DomainError(FatoValorDominioErrorCodes.DescricaoTamanho,
                $"Descrição do valor de domínio deve ter no máximo {FatoValorDominio.DescricaoMaxLength} caracteres.")));
        }
        else if (Origem == OrigemFato.Declarado && descricaoNormalizada is null)
        {
            erros.Add(new("descricao", new DomainError(FatoValorDominioErrorCodes.DescricaoObrigatoria,
                "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO.")));
        }

        string? orientacaoNormalizada = string.IsNullOrWhiteSpace(orientacao) ? null : orientacao.Trim();
        if (orientacaoNormalizada is { Length: > FatoValorDominio.OrientacaoMaxLength })
        {
            erros.Add(new("orientacao", new DomainError(FatoValorDominioErrorCodes.OrientacaoTamanho,
                $"Orientação do valor de domínio deve ter no máximo {FatoValorDominio.OrientacaoMaxLength} caracteres.")));
        }

        return (descricaoNormalizada, orientacaoNormalizada);
    }

    private static readonly DomainError ValorNaoEncontrado =
        new(FatoValorDominioErrorCodes.NaoEncontrado, "Valor de domínio não encontrado neste fato.");

    private FatoValorDominio? ValorPorCodigo(string codigo) =>
        _valoresDominioDeclarados.FirstOrDefault(v => string.Equals(v.Codigo, codigo?.Trim(), StringComparison.Ordinal));

    private DomainError? RecusaSeSistema() =>
        Sistema
            ? new DomainError(
                FatoCandidatoErrorCodes.FatoDeSistemaSoEditaNomeEDescricao,
                "Fato de sistema só tem o nome e a descrição editáveis.")
            : null;

    private static Result<(string Nome, string? Descricao)> ValidarDescritivo(string nome, string? descricao)
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

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > DescricaoMaxLength })
        {
            erros.Add(new("descricao", new DomainError(
                FatoCandidatoErrorCodes.DescricaoTamanho, $"Descrição do fato deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        return erros.Count > 0
            ? Result<(string, string?)>.ValidationFailure(erros)
            : Result<(string, string?)>.Success((nomeNormalizado, descricaoNormalizada));
    }

    /// <summary>
    /// O domínio que o derivado por regra aceita, no cadastro e na alteração: ausente é obrigatório,
    /// fora do vocabulário é inválido e o válido que não é booleano nem categórico não tem regra.
    /// </summary>
    private static bool ConferirDominioDoDerivado(List<FieldError> erros, DominioFato dominio)
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

    private static Result<ProtecaoDeDados> ValidarProtecao(
        DominioFato? dominio, ClassificacaoDeProtecao classificacao, string finalidade, HipoteseLegalTratamento hipotese, bool nomeSocialPublico) =>
        ProtecaoDeDados.Validar(
            classificacao, finalidade, hipotese, RecusasDaProtecaoDoFato,
            // Texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), data e
            // endereço identificam ou localizam a pessoa: nunca são menos que dado pessoal. Texto
            // também aceita `Identificador` — número de documento é sempre texto, nunca outro
            // domínio (ADR-0136, emenda de #1857). A exceção é o nome social de sistema, texto
            // público (ADR-0082, ADR-0136).
            nivel => !nomeSocialPublico && dominio is { } dominioConferido && !AtendeClassificacaoMinimaDoDominio(dominioConferido, nivel)
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
}
