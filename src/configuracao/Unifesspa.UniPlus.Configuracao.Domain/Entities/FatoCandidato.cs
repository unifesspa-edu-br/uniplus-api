namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
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
    private const int FinalidadeTratamentoMaxLength = 500;

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
    public ClassificacaoProtecaoDado ClassificacaoProtecao { get; private set; }

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
        ClassificacaoProtecaoDado classificacaoProtecao,
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

        bool dominioValido = dominio != DominioFato.Nenhum && Enum.IsDefined(dominio);
        if (dominio == DominioFato.Nenhum)
        {
            Recusar("dominio", FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório.");
        }
        else if (!dominioValido)
        {
            Recusar("dominio", FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado.");
        }

        bool origemValida = origem != OrigemFato.Nenhuma && Enum.IsDefined(origem);
        if (origem == OrigemFato.Nenhuma)
        {
            Recusar("origem", FatoCandidatoErrorCodes.OrigemObrigatoria, "Origem do fato é obrigatória.");
        }
        else if (!origemValida)
        {
            Recusar("origem", FatoCandidatoErrorCodes.OrigemInvalida, "Origem do fato fora do vocabulário fechado.");
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

        if (escopo == EscopoFato.Nenhum || !Enum.IsDefined(escopo))
        {
            Recusar("escopo", FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato é obrigatório.");
        }

        Result<ProtecaoValidada> protecao = ValidarProtecao(
            dominio, classificacaoProtecao, finalidadeTratamento, hipoteseLegal,
            nomeSocialPublico: sistema
                && string.Equals(codigo?.Trim(), CodigoDoNomeSocial, StringComparison.Ordinal)
                && dominio == DominioFato.Texto
                && classificacaoProtecao == ClassificacaoProtecaoDado.Publico);
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
        ClassificacaoProtecaoDado classificacaoProtecao,
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
        ClassificacaoProtecaoDado classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal)
    {
        bool categorico = dominio == DominioFato.Categorico;
        Result<FatoCandidato> criado = Criar(
            codigo, nome, descricao, dominio, OrigemFato.Derivado,
            categorico ? CardinalidadeFato.Multivalorado : CardinalidadeFato.Escalar,
            categorico ? FonteValoresFato.Global : null,
            formato: null, pontoResolucao, $"{PrefixoBindingDerivadoRegra}:{codigo?.Trim()}", escopo,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false);
        if (dominio is DominioFato.Booleano or DominioFato.Categorico)
        {
            return criado;
        }

        // Com o domínio recusado, o que depende dele (formato, fonte) não é conferido.
        FieldError recusa = new("dominio", new DomainError(
            FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico,
            "Fato derivado por regra é booleano ou categórico."));
        return Result<FatoCandidato>.ValidationFailure(
            [recusa, .. criado.Errors.Where(static e => e.Field is not ("dominio" or "formato" or "fonteValores"))]);
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
        ClassificacaoProtecaoDado classificacaoProtecao,
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
        bool categorico = operacao == OperacaoAgregado.ValoresPresentes;
        Result<FatoCandidato> criado = Criar(
            codigo, nome, descricao,
            categorico ? DominioFato.Categorico : DominioFato.Booleano,
            OrigemFato.Derivado,
            categorico ? CardinalidadeFato.Multivalorado : CardinalidadeFato.Escalar,
            categorico ? membro!.FonteValores : null,
            formato: null, pontoResolucao, $"{PrefixoBindingAgregacao}:{membroCodigo}", EscopoFato.Candidato,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema: false);

        // As recusas que dependem do fato de membro só valem com ele válido; sem ele, o domínio e a
        // fonte não foram inferidos e as recusas desses campos não orientam.
        erros.AddRange(membroValido ? criado.Errors : criado.Errors.Where(static e => e.Field is not ("dominio" or "fonteValores" or "binding")));
        // A proteção e o ponto de resolução se comparam com os do fato de membro sempre que o campo
        // é válido, independentemente das recusas dos outros campos.
        if (membroValido)
        {
            if (classificacaoProtecao != ClassificacaoProtecaoDado.Nenhuma && Enum.IsDefined(classificacaoProtecao)
                && classificacaoProtecao < membro!.ClassificacaoProtecao)
            {
                Recusar("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia,
                    $"O agregado revela o que se sabe de '{membroCodigo}' e não pode ter proteção de dados mais fraca que a dele.");
            }

            string ponto = pontoResolucao?.Trim() ?? string.Empty;
            if (FaseCanonicaCatalogo.EhCanonico(ponto) && ValidadorRegrasPadrao.Precede(ponto, membro!.PontoResolucao, catalogo.Precedencias))
            {
                Recusar("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia,
                    $"O agregado resolve em {ponto}, antes de '{membroCodigo}', que só é conhecido em {membro.PontoResolucao}.");
            }
        }

        return erros.Count > 0 ? Result<FatoCandidato>.ValidationFailure(erros) : criado;
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

        FatoValorDominio? valor = _valoresDominioDeclarados.FirstOrDefault(v =>
            string.Equals(v.Codigo, codigo?.Trim(), StringComparison.Ordinal));
        if (valor is null)
        {
            return Result.Failure(new DomainError(FatoValorDominioErrorCodes.NaoEncontrado, "Valor de domínio não encontrado neste fato."));
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
    /// Altera o nome e a descrição. Os eixos do fato — domínio, origem, cardinalidade, escopo,
    /// fonte, ponto de resolução, vínculo e proteção de dados — não se editam depois do cadastro:
    /// mudança num eixo é um fato novo (ADR-0136).
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

        if (FatoDeMembroAgregado is { } membro)
        {
            return Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios,
                $"Os valores do agregado são os do fato de membro '{membro}'; acrescente-os nele."));
        }

        if (FonteValores != FonteValoresFato.Global)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal,
                "Valores de domínio só são declarados no catálogo quando a fonte dos valores é global; "
                + "nas demais fontes, os valores vêm do processo."));
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

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > FatoValorDominio.DescricaoMaxLength })
        {
            Recusar("descricao", FatoValorDominioErrorCodes.DescricaoTamanho,
                $"Descrição do valor de domínio deve ter no máximo {FatoValorDominio.DescricaoMaxLength} caracteres.");
        }
        else if (Origem == OrigemFato.Declarado && descricaoNormalizada is null)
        {
            Recusar("descricao", FatoValorDominioErrorCodes.DescricaoObrigatoria,
                "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO.");
        }

        string? orientacaoNormalizada = string.IsNullOrWhiteSpace(orientacao) ? null : orientacao.Trim();
        if (orientacaoNormalizada is { Length: > FatoValorDominio.OrientacaoMaxLength })
        {
            Recusar("orientacao", FatoValorDominioErrorCodes.OrientacaoTamanho,
                $"Orientação do valor de domínio deve ter no máximo {FatoValorDominio.OrientacaoMaxLength} caracteres.");
        }

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

    private static Result<ProtecaoValidada> ValidarProtecao(
        DominioFato dominio, ClassificacaoProtecaoDado classificacao, string finalidade, HipoteseLegalTratamento hipotese, bool nomeSocialPublico)
    {
        List<FieldError> erros = [];
        void Recusar(string campo, string codigo, string mensagem) => erros.Add(new(campo, new DomainError(codigo, mensagem)));

        bool classificacaoValida = classificacao != ClassificacaoProtecaoDado.Nenhuma && Enum.IsDefined(classificacao);
        if (!classificacaoValida)
        {
            Recusar("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria,
                "Classificação de proteção de dados do fato é obrigatória.");
        }
        else if (!nomeSocialPublico && !AtendeClassificacaoMinimaDoDominio(dominio, classificacao))
        {
            // Texto (em todo formato, inclusive o livre, que pode conter qualquer coisa), data e
            // endereço identificam ou localizam a pessoa: nunca são menos que dado pessoal. Texto
            // também aceita `Identificador` — número de documento é sempre texto (ADR-0136,
            // emenda de #1857). A exceção é o nome social de sistema, texto público (ADR-0082, ADR-0136).
            Recusar("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio,
                "Fato de texto, data ou endereço é classificado como pessoal, identificador (só texto) ou sensível.");
        }

        string finalidadeNormalizada = finalidade?.Trim() ?? string.Empty;
        if (finalidadeNormalizada.Length == 0)
        {
            Recusar("finalidadeTratamento", FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria,
                "Finalidade do tratamento do fato é obrigatória.");
        }
        else if (finalidadeNormalizada.Length > FinalidadeTratamentoMaxLength)
        {
            Recusar("finalidadeTratamento", FatoCandidatoErrorCodes.FinalidadeTratamentoTamanho,
                $"Finalidade do tratamento deve ter no máximo {FinalidadeTratamentoMaxLength} caracteres.");
        }

        if (hipotese == HipoteseLegalTratamento.Nenhuma || !Enum.IsDefined(hipotese))
        {
            Recusar("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalObrigatoria, "Hipótese legal de tratamento do fato é obrigatória.");
        }
        else if (classificacaoValida && !HipotesesLegaisTratamento.AdmiteClassificacao(hipotese, classificacao))
        {
            Recusar("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao,
                "A hipótese legal não cabe na classificação: dado sensível usa as hipóteses do art. 11 da LGPD, e os demais as do art. 7º.");
        }

        return erros.Count > 0
            ? Result<ProtecaoValidada>.ValidationFailure(erros)
            : Result<ProtecaoValidada>.Success(new ProtecaoValidada(classificacao, finalidadeNormalizada, hipotese));
    }

    private static bool AtendeClassificacaoMinimaDoDominio(DominioFato dominio, ClassificacaoProtecaoDado classificacao) =>
        dominio switch
        {
            DominioFato.Texto => classificacao is ClassificacaoProtecaoDado.Pessoal
                or ClassificacaoProtecaoDado.Identificador or ClassificacaoProtecaoDado.Sensivel,
            DominioFato.Data or DominioFato.Endereco => classificacao is ClassificacaoProtecaoDado.Pessoal
                or ClassificacaoProtecaoDado.Sensivel,
            _ => true,
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

    /// <summary>A proteção de dados já validada, aplicada na criação.</summary>
    private readonly record struct ProtecaoValidada(
        ClassificacaoProtecaoDado Classificacao, string Finalidade, HipoteseLegalTratamento Hipotese);
}
