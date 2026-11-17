namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Instituição de ensino superior dona da plataforma — a Unifesspa no
/// deployment da Unifesspa (ADR-0055). Carrega a identificação regulatória
/// e-MEC (código, classificação, mantenedora, atos de credenciamento,
/// indicadores de qualidade) que serve de cabeçalho institucional exibido em
/// editais, comprovantes e telas administrativas.
/// </summary>
/// <remarks>
/// <para><strong>Singleton</strong> (UNI-REQ-0007 · ADR-0055): cada deployment
/// atende uma única instituição, logo há no máximo uma <c>Instituicao</c> viva
/// (não soft-deleted) por instância. O cardinal de um é garantido em duas
/// camadas — guard de domínio na criação (o handler consulta o repositório
/// antes de criar) e índice único parcial de banco sobre uma coluna sentinela
/// constante <c>WHERE is_deleted = false</c>. A remoção é lógica; uma
/// Instituição removida não conta para o limite, liberando o recadastramento.</para>
/// <para>Não há unicidade própria sobre <c>codigo_emec</c>: sob o singleton, o
/// cardinal de um já garante a identidade (o dicionário marca a coluna como
/// <c>text NOT NULL</c>, sem <c>UNIQUE</c>).</para>
/// <para>A vinculação com a <c>Unidade</c> raiz (reitoria) é por
/// <c>UnidadeRaizId</c> — FK intra-banco (ADR-0054). A conferência de que a
/// Unidade referenciada é viva e do tipo reitoria é responsabilidade do handler
/// (via repositório); esta entidade recebe o Id já validado.</para>
/// <para>O município da sede é uma <strong>referência de cidade do Geo</strong>
/// (ADR-0090): <c>CidadeCodigoIbge</c> (código IBGE de 7 dígitos) + display cache
/// (<c>CidadeNome</c>, <c>CidadeUf</c>), preenchido pelo frontend via composição
/// no cliente — sem FK cross-banco nem chamada ao Geo. É <strong>opcional</strong>
/// (all-or-nothing): ou o trio completo e coerente, ou ausente por completo.</para>
/// <para>O <see cref="Endereco"/> da sede é uma referência de endereço estruturado
/// ao Geo via CEP (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096), opcional —
/// sucede o antigo <c>EnderecoSede</c> texto livre. O Geo <strong>modela</strong>
/// endereço pontual (lookup de CEP #676, busca de logradouro #707); quando há
/// endereço, a referência de cidade da sede é obrigatória e coerente com ele.</para>
/// </remarks>
public sealed class Instituicao : SoftDeletableEntity, IAuditableEntity
{

    public string CodigoEmec { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Sigla { get; private set; } = string.Empty;
    public string OrganizacaoAcademica { get; private set; } = string.Empty;
    public string CategoriaAdministrativa { get; private set; } = string.Empty;
    public string? Cnpj { get; private set; }
    public string? Mantenedora { get; private set; }
    public string? CodigoMantenedoraEmec { get; private set; }
    public string? Situacao { get; private set; }
    public string? AtoCredenciamento { get; private set; }
    public string? AtoRecredenciamento { get; private set; }
    public string? ConceitoInstitucional { get; private set; }
    public string? Igc { get; private set; }
    public string? Website { get; private set; }

    // Referência de cidade do Geo (ADR-0090) — código + display cache, opcional
    // (all-or-nothing). Substitui o antigo MunicipioSede texto livre.
    public string? CidadeCodigoIbge { get; private set; }
    public string? CidadeNome { get; private set; }
    public string? CidadeUf { get; private set; }
    public string? CidadeOrigem { get; private set; }
    public DateTimeOffset? CidadeDisplayAtualizadoEm { get; private set; }

    // Endereço estruturado ao Geo via CEP (ADR-0096) — opcional, owned type.
    // Sucede o antigo EnderecoSede texto livre. Quando presente, exige a cidade
    // da sede preenchida e coerente.
    public ReferenciaEnderecoGeo? Endereco { get; private set; }

    public Guid? UnidadeRaizId { get; private set; }

    /// <summary>
    /// Sentinela constante (sempre <see langword="true"/>) — concern de
    /// persistência que materializa a salvaguarda física do singleton: o índice
    /// único parcial <c>WHERE is_deleted = false</c> sobre esta coluna admite no
    /// máximo um registro vivo (ADR-0055), e o CHECK de banco impede qualquer
    /// outro valor. Não é dado de negócio; não é exposto em DTO/View.
    /// </summary>
    public bool RegistroVivoSentinela { get; private set; } = true;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Instituicao()
    {
    }

    /// <summary>
    /// A referência de cidade da sede como o agregado a guarda, montada das colunas planas. Não é
    /// persistida: serve para comparar com a cidade informada e decidir o carimbo.
    /// </summary>
    public CidadeDaSede CidadeDaSede =>
        new(CidadeCodigoIbge, CidadeNome, CidadeUf, CidadeOrigem, CidadeDisplayAtualizadoEm);

    /// <summary>
    /// Cria a Instituição. Valida apenas formato e domínio local (incluindo a
    /// referência de cidade opcional) — o invariante singleton e a conferência do
    /// tipo da Unidade raiz são responsabilidade do handler chamador (dependem do
    /// repositório). A proveniência/frescura do display cache da cidade é carimbada
    /// server-side pelo handler (<see cref="CidadeDaSede.Carimbada"/>, ADR-0090).
    /// </summary>
    public static Result<Instituicao> Criar(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade,
        ReferenciaEnderecoGeo? endereco,
        Guid? unidadeRaizId)
    {
        Result validacao = ValidarCampos(identificacao, cidade, endereco);
        if (validacao.IsFailure)
        {
            return Result<Instituicao>.ValidationFailure(validacao.Errors);
        }

        var instituicao = new Instituicao();
        instituicao.AplicarCampos(identificacao, cidade, endereco, unidadeRaizId);

        return Result<Instituicao>.Success(instituicao);
    }

    /// <summary>
    /// Atualiza os dados regulatórios e o vínculo com a reitoria. A conferência
    /// do tipo da Unidade raiz informada é responsabilidade do handler, assim
    /// como o carimbo da proveniência/frescura do display cache da cidade.
    /// </summary>
    public Result Atualizar(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade,
        ReferenciaEnderecoGeo? endereco,
        Guid? unidadeRaizId)
    {
        Result validacao = ValidarCampos(identificacao, cidade, endereco);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(identificacao, cidade, endereco, unidadeRaizId);

        return Result.Success();
    }

    /// <summary>
    /// Valida todos os campos da Instituição, acumulando cada violação em vez de
    /// retornar na primeira (ADR-0125) — o array <c>errors[]</c> do contrato
    /// público (ADR-0023) precisa de todas as regras de campo violadas no mesmo
    /// lote, não só a primeira. Público para o handler de atualização poder
    /// validar o payload inteiro antes de buscar o registro por Id (validação
    /// sempre vence 404) — mesmo uso que o handler de criação faz antes do guard
    /// de singleton e do vínculo com a Unidade raiz.
    /// </summary>
    public static Result ValidarCampos(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade,
        ReferenciaEnderecoGeo? endereco)
        => ConferenciaDaInstituicao.Conferir(identificacao, cidade, endereco);

    private void AplicarCampos(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade,
        ReferenciaEnderecoGeo? endereco,
        Guid? unidadeRaizId)
    {
        AplicarIdentificacao(identificacao.Normalizar());
        AplicarCidade(cidade.Normalizada());
        Endereco = endereco;
        UnidadeRaizId = unidadeRaizId;
    }

    private void AplicarIdentificacao(IdentificacaoRegulatoriaDaInstituicao identificacao)
    {
        CodigoEmec = identificacao.CodigoEmec!;
        Nome = identificacao.Nome!;
        Sigla = identificacao.Sigla!;
        OrganizacaoAcademica = identificacao.OrganizacaoAcademica!;
        CategoriaAdministrativa = identificacao.CategoriaAdministrativa!;
        Cnpj = identificacao.Cnpj;
        Mantenedora = identificacao.Mantenedora;
        CodigoMantenedoraEmec = identificacao.CodigoMantenedoraEmec;
        Situacao = identificacao.Situacao;
        AtoCredenciamento = identificacao.AtoCredenciamento;
        AtoRecredenciamento = identificacao.AtoRecredenciamento;
        ConceitoInstitucional = identificacao.ConceitoInstitucional;
        Igc = identificacao.Igc;
        Website = identificacao.Website;
    }

    private void AplicarCidade(CidadeDaSede cidade)
    {
        CidadeCodigoIbge = cidade.CodigoIbge;
        CidadeNome = cidade.Nome;
        CidadeUf = cidade.Uf;
        CidadeOrigem = cidade.Origem;
        CidadeDisplayAtualizadoEm = cidade.DisplayAtualizadoEm;
    }
}
