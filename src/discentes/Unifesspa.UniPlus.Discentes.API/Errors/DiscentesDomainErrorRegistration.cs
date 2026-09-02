namespace Unifesspa.UniPlus.Discentes.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Mapeia os erros de domínio do módulo Discentes para a resposta de erro padronizada.
/// </summary>
/// <remarks>
/// <para>
/// Todos respondem como falha interna, e não como erro de requisição, porque nenhum deles
/// nasce de algo que um cliente da API pediu: são recusas de dado vindo do sistema
/// acadêmico durante a sincronização. Se um deles alcançar uma resposta HTTP, quem errou
/// foi a integração, não quem chamou — e o código precisa dizer isso.
/// </para>
/// <para>
/// Erro de domínio sem registro aqui vira uma resposta genérica que não identifica a
/// causa. Por isso o registro cobre também os erros que hoje não têm como chegar a uma
/// resposta: o módulo ainda não expõe endpoints, e é justamente enquanto isso é verdade
/// que a lacuna passa despercebida.
/// </para>
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, DiscentesDomainErrorRegistration>().")]
internal sealed class DiscentesDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(DiscentesErrorCodes.Curso.IdInvalido,
            Interno("uniplus.discentes.curso.id_invalido",
                "Curso do vínculo veio da origem sem identificador válido")),

        new(DiscentesErrorCodes.Curso.NomeVazio,
            Interno("uniplus.discentes.curso.nome_vazio",
                "Curso do vínculo veio da origem sem nome")),

        new(DiscentesErrorCodes.Curso.UnidadeIdInvalido,
            Interno("uniplus.discentes.curso.unidade_id_invalido",
                "Unidade acadêmica do curso veio da origem sem identificador válido")),

        new(DiscentesErrorCodes.Curso.UnidadeNomeVazio,
            Interno("uniplus.discentes.curso.unidade_nome_vazio",
                "Unidade acadêmica do curso veio da origem sem nome")),

        new(DiscentesErrorCodes.Curso.NomeLongo,
            Interno("uniplus.discentes.curso.nome_longo",
                "Nome do curso veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.Curso.CodigoEmecLongo,
            Interno("uniplus.discentes.curso.codigo_emec_longo",
                "Código e-MEC do curso veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.Curso.UnidadeNomeLongo,
            Interno("uniplus.discentes.curso.unidade_nome_longo",
                "Nome da unidade acadêmica veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.PeriodoIngresso.AnoInvalido,
            Interno("uniplus.discentes.periodo_ingresso.ano_invalido",
                "Ano de ingresso do vínculo veio da origem fora do intervalo aceito")),

        new(DiscentesErrorCodes.PeriodoIngresso.PeriodoInvalido,
            Interno("uniplus.discentes.periodo_ingresso.periodo_invalido",
                "Período de ingresso do vínculo veio da origem fora do intervalo aceito")),

        new(DiscentesErrorCodes.SituacaoAcademica.IdInvalido,
            Interno("uniplus.discentes.situacao_academica.id_invalido",
                "Situação acadêmica do vínculo veio da origem sem identificador válido")),

        new(DiscentesErrorCodes.SituacaoAcademica.DescricaoVazia,
            Interno("uniplus.discentes.situacao_academica.descricao_vazia",
                "Situação acadêmica do vínculo veio da origem sem descrição")),

        new(DiscentesErrorCodes.SituacaoAcademica.DescricaoLonga,
            Interno("uniplus.discentes.situacao_academica.descricao_longa",
                "Descrição da situação veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.SituacaoAcademica.VinculoLongo,
            Interno("uniplus.discentes.situacao_academica.vinculo_longo",
                "Qualificador da situação veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.VinculoDiscente.IdSigaaInvalido,
            Interno("uniplus.discentes.vinculo.id_origem_invalido",
                "Vínculo veio da origem sem identificador válido")),

        new(DiscentesErrorCodes.VinculoDiscente.MatriculaVazia,
            Interno("uniplus.discentes.vinculo.matricula_vazia",
                "Vínculo veio da origem sem matrícula")),

        new(DiscentesErrorCodes.VinculoDiscente.MatriculaLonga,
            Interno("uniplus.discentes.vinculo.matricula_longa",
                "Matrícula veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.VinculoDiscente.MatriculaNaoNumerica,
            Interno("uniplus.discentes.vinculo.matricula_nao_numerica",
                "Matrícula veio da origem com caracteres que não são dígitos")),

        new(DiscentesErrorCodes.VinculoDiscente.NomeVazio,
            Interno("uniplus.discentes.vinculo.nome_vazio",
                "Vínculo veio da origem sem nome do discente")),

        new(DiscentesErrorCodes.VinculoDiscente.NomeLongo,
            Interno("uniplus.discentes.vinculo.nome_longo",
                "Nome do discente veio da origem maior do que a réplica comporta")),

        new(DiscentesErrorCodes.VinculoDiscente.NivelVazio,
            Interno("uniplus.discentes.vinculo.nivel_vazio",
                "Vínculo veio da origem sem nível de ensino")),

        new(DiscentesErrorCodes.VinculoDiscente.NivelLongo,
            Interno("uniplus.discentes.vinculo.nivel_longo",
                "Nível de ensino veio da origem maior do que a réplica comporta")),
    ];

    private static DomainErrorMapping Interno(string codigo, string titulo) =>
        new(StatusCodes.Status500InternalServerError, codigo, titulo);
}
