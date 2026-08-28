namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Campus institucional — unidade física onde a instituição oferta cursos
/// (UNI-REQ #587, módulo Configuração). Referencia a cidade do módulo
/// <c>Geo</c> por <c>CidadeCodigoIbge</c> (código IBGE de 7 dígitos) + display
/// cache (<c>CidadeNome</c>, <c>CidadeUf</c>), preenchido pelo frontend via
/// composição no cliente (ADR-0090) — sem FK cross-banco nem chamada ao Geo.
/// </summary>
/// <remarks>
/// <para>A <c>Sigla</c> é única entre campi vivos (não soft-deleted); a
/// unicidade é validada pelo handler antes da factory e reforçada por índice
/// único parcial de banco (<c>WHERE is_deleted = false</c>).</para>
/// <para>O <see cref="Endereco"/> é uma referência de endereço estruturado ao
/// Geo via CEP, opcional (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096) — sucede
/// o antigo trio texto-livre <c>Endereco</c>/<c>Cep</c>/coordenada. Quando
/// presente, seu snapshot de cidade deve ser coerente com a referência de cidade
/// do campus (CA-04).</para>
/// <para>O congelamento (snapshot RN08) é responsabilidade do Processo Seletivo
/// (módulo Selecao, ADR-0061) — não há colunas de snapshot aqui.</para>
/// </remarks>
public sealed class Campus : SoftDeletableEntity, IAuditableEntity
{
    private const int SiglaMinLength = 1;
    private const int SiglaMaxLength = 20;
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int CodigoEmecMaxLength = 20;

    public string Sigla { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    // Referência de cidade do Geo (ADR-0090) — código + display cache.
    public string CidadeCodigoIbge { get; private set; } = string.Empty;
    public string CidadeNome { get; private set; } = string.Empty;
    public string CidadeUf { get; private set; } = string.Empty;
    public string? CidadeOrigem { get; private set; }
    public DateTimeOffset? CidadeDisplayAtualizadoEm { get; private set; }

    // Endereço estruturado ao Geo via CEP (ADR-0096) — opcional, owned type.
    public ReferenciaEnderecoGeo? Endereco { get; private set; }

    public string? CodigoEmec { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Campus()
    {
    }

    /// <summary>
    /// Cria um novo Campus. Valida formato e domínio local (incluindo a
    /// referência de cidade via <see cref="ReferenciaCidadeGeo"/> e a coerência
    /// cidade↔endereço). A unicidade de <paramref name="sigla"/> entre campi
    /// vivos é responsabilidade do handler. O <paramref name="endereco"/> já
    /// chega validado (construído pelo handler via <see cref="ReferenciaEnderecoGeo.Criar"/>).
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Nulo em sigla/nome/cidade é violação de campo (\"obrigatório\"), não bug de " +
            "contrato — ValidarCampos trata via IsNullOrWhiteSpace e devolve Result.ValidationFailure, " +
            "não ArgumentNullException (ADR-0125: domínio é fonte única de validação, sem validator " +
            "FluentValidation garantindo não-nulo a montante).")]
    public static Result<Campus> Criar(
        string? sigla,
        string? nome,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        // Assinatura nullable de propósito (ADR-0125): sem validator FluentValidation
        // garantindo não-nulo a montante, domínio é fonte única — nulo é
        // Result.ValidationFailure("obrigatório"), não ArgumentNullException/500.
        // ValidarCampos trata nulo e vazio da mesma forma (IsNullOrWhiteSpace); os
        // parâmetros só chegam não-nulos em AplicarCampos porque essa checagem já
        // passou.

        Result validacao = ValidarCampos(sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return Result<Campus>.ValidationFailure(validacao.Errors);
        }

        var campus = new Campus();
        campus.AplicarCampos(
            sigla!, nome!, cidadeCodigoIbge!, cidadeNome!, cidadeUf!, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, codigoEmec);

        return Result<Campus>.Success(campus);
    }

    /// <summary>
    /// Valida os campos de uma atualização sem mutar o agregado. Existe porque o
    /// handler de atualização precisa confirmar unicidade de <c>Sigla</c> antes
    /// de mutar: o agregado chega já rastreado pelo EF (via <c>ObterPorIdAsync</c>),
    /// e o Wolverine roda <c>SaveChangesAsync</c> depois do handler retornar
    /// mesmo quando o <see cref="Result"/> devolvido é falha — mutar antes de
    /// confirmar a unicidade persistiria a sigla em conflito apesar do 409.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Ver justificativa equivalente em Criar.")]
    public static Result ValidarAtualizacao(
        string? sigla,
        string? nome,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec) =>
        ValidarCampos(sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);

    /// <summary>
    /// Atualiza os atributos do Campus. A unicidade de <paramref name="sigla"/>
    /// (quando alterada) é responsabilidade do handler, que precisa confirmá-la
    /// com <see cref="ValidarAtualizacao"/> antes de chamar este método — ver o
    /// comentário ali sobre por que a mutação não pode acontecer antes disso.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Ver justificativa equivalente em Criar.")]
    public Result Atualizar(
        string? sigla,
        string? nome,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        // Ver comentário equivalente em Criar: nulo é violação de campo, não bug
        // de contrato — precisa virar ValidationFailure, não ArgumentNullException.

        Result validacao = ValidarCampos(sigla, nome, cidadeCodigoIbge, cidadeNome, cidadeUf, endereco, codigoEmec);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            sigla!, nome!, cidadeCodigoIbge!, cidadeNome!, cidadeUf!, cidadeOrigem, cidadeDisplayAtualizadoEm,
            endereco, codigoEmec);

        return Result.Success();
    }

    private void AplicarCampos(
        string sigla,
        string nome,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        Sigla = sigla.Trim().ToUpperInvariant();
        Nome = nome.Trim();
        CidadeCodigoIbge = cidadeCodigoIbge.Trim();
        CidadeNome = cidadeNome.Trim();
        CidadeUf = cidadeUf.Trim().ToUpperInvariant();
        CidadeOrigem = NormalizarOpcional(cidadeOrigem);
        CidadeDisplayAtualizadoEm = cidadeDisplayAtualizadoEm;
        Endereco = endereco;
        CodigoEmec = NormalizarOpcional(codigoEmec);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Valida todos os campos do Campus, acumulando cada violação em vez de
    /// retornar na primeira (ADR-0125) — o array <c>errors[]</c> do contrato
    /// público (ADR-0023) precisa de todas as regras de campo violadas no mesmo
    /// lote, não só a primeira. Cidade/endereço delegam a
    /// <see cref="ReferenciaCidadeGeo"/>/<see cref="ReferenciaEnderecoGeo"/>
    /// (fonte única compartilhada com os demais cadastros que referenciam cidade)
    /// e contribuem no máximo um erro cada.
    /// </summary>
    private static Result ValidarCampos(
        string? sigla,
        string? nome,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        ReferenciaEnderecoGeo? endereco,
        string? codigoEmec)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(sigla))
        {
            erros.Add(new("sigla", new DomainError(
                CampusErrorCodes.SiglaObrigatoria, "Sigla do Campus é obrigatória.")));
        }
        else if (sigla.Trim().Length is < SiglaMinLength or > SiglaMaxLength)
        {
            erros.Add(new("sigla", new DomainError(
                CampusErrorCodes.SiglaTamanho,
                $"Sigla do Campus deve ter entre {SiglaMinLength} e {SiglaMaxLength} caracteres.")));
        }
        else if (ContemAcentuacaoGrafica(sigla.Trim()))
        {
            // Recusa em vez de remover o acento: transformar "CÁMAR" em "CAMAR"
            // alteraria o valor informado e poderia colidir com a sigla de outro
            // Campus vivo. Mensagem sem ecoar o valor rejeitado (ADR-0023).
            erros.Add(new("sigla", new DomainError(
                CampusErrorCodes.SiglaAcentuacaoInvalida,
                "A sigla do Campus não pode conter acentuação gráfica.")));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                CampusErrorCodes.NomeObrigatorio, "Nome do Campus é obrigatório.")));
        }
        else if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                CampusErrorCodes.NomeTamanho,
                $"Nome do Campus deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
        }

        Result cidade = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            // ReferenciaCidadeGeo.Validar acumula toda violação independente do
            // trio de cidade (ex.: código, nome e UF ausentes ao mesmo tempo
            // viram três erros) — cada um mapeado para o campo real que descreve.
            foreach (FieldError erroCidade in cidade.Errors)
            {
                erros.Add(new(CampoDaCidade(erroCidade.Error.Code), erroCidade.Error));
            }
        }
        else
        {
            // CA-04: só faz sentido checar coerência quando a referência de cidade em
            // si já é válida — senão a causa raiz (formato) fica mascarada por
            // "incoerente".
            Result coerencia = ReferenciaEnderecoGeo.ValidarCoerencia(
                endereco?.CidadeCodigoIbge, endereco?.CidadeUf, cidadeCodigoIbge, cidadeUf);
            if (coerencia.IsFailure)
            {
                erros.Add(new("endereco", coerencia.Error!));
            }
        }

        if (codigoEmec is not null && codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            erros.Add(new("codigoEmec", new DomainError(
                CampusErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC do Campus deve ter no máximo {CodigoEmecMaxLength} caracteres.")));
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Indica se <paramref name="valor"/> contém acentuação gráfica — qualquer
    /// diacrítico, esteja ele pré-composto (<c>Á</c>, U+00C1) ou já recebido como
    /// letra seguida de marca combinante (<c>A</c> + U+0301). A decomposição
    /// canônica (NFD) reduz os dois casos ao mesmo sinal: uma marca sem avanço de
    /// largura (<see cref="UnicodeCategory.NonSpacingMark"/>), categoria em que
    /// caem agudo, grave, circunflexo, til, trema e cedilha — a mesma que o
    /// restante do repositório já usa para reconhecer diacrítico.
    /// </summary>
    /// <remarks>
    /// <para>Iterar por <see cref="Rune"/> em vez de <see cref="char"/> tem duas
    /// razões: um diacrítico fora do plano básico chega como par substituto e
    /// escaparia de uma checagem char a char; e <c>EnumerateRunes</c> substitui
    /// par substituto malformado por U+FFFD, de modo que uma sigla mal-formada
    /// segue para as demais regras em vez de estourar a
    /// <see cref="ArgumentException"/> que <c>String.Normalize</c> lançaria — 422,
    /// nunca 500. Pela mesma razão a decomposição é feita caractere a caractere,
    /// não sobre a string inteira.</para>
    /// <para>O recorte é toda marca sem avanço de largura, deliberadamente. Ele
    /// deixa de fora as marcas combinantes de outros sistemas de escrita
    /// (categorias <c>Mc</c> e <c>Me</c>), que não são acentuação gráfica, e
    /// abrange alguns caracteres invisíveis que compartilham a categoria sem
    /// acentuar coisa alguma — seletor de variação, juntor de grafema. Recusá-los
    /// é o resultado desejado: a sigla é identificador institucional, e um
    /// caractere invisível nela produz exatamente a variação visualmente
    /// indistinguível que esta regra existe para evitar. Fora do recorte, a regra
    /// não redefine os demais caracteres aceitos — hífen e dígitos seguem
    /// válidos.</para>
    /// </remarks>
    private static bool ContemAcentuacaoGrafica(string valor) =>
        valor.EnumerateRunes().Any(static caractere =>
            caractere.ToString().Normalize(NormalizationForm.FormD).EnumerateRunes()
                .Any(static parte => Rune.GetUnicodeCategory(parte) == UnicodeCategory.NonSpacingMark));

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o
    /// campo do payload (camelCase, ADR-0023) a que ele se refere de fato — sem
    /// isso, todo erro de cidade seria rotulado com o mesmo campo em
    /// <c>errors[].field</c>, mesmo quando a causa é o nome ou a UF, não o código
    /// IBGE.
    /// </summary>
    private static string CampoDaCidade(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };
}
