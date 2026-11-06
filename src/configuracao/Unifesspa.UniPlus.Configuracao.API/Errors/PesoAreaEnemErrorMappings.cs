namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Pesos por Área do ENEM, por grupo de área.</summary>
internal static class PesoAreaEnemErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(PesoAreaEnemErrorCodes.ResolucaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.resolucao_obrigatoria",
                "Resolução é obrigatória")),

        new(PesoAreaEnemErrorCodes.ResolucaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.resolucao_tamanho",
                "Tamanho da resolução inválido")),

        new(PesoAreaEnemErrorCodes.ResolucaoCaractereInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.resolucao_caractere_invalido",
                "A resolução contém caractere de controle, de formatação, de quebra de linha ou que não é texto")),

        new(GrupoCursoErrorCodes.ForaDoDominio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.grupo_curso.fora_do_dominio",
                "Grupo de área do ENEM fora dos quatro grupos da Resolução nº 805/2024/Consepe")),

        new(PesoAreaEnemErrorCodes.GrupoCursoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.grupo_curso_invalido",
                "Grupo de curso fora do domínio de grupos de área do ENEM")),

        new(PesoAreaEnemErrorCodes.ParJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.peso_area_enem.par_ja_existe",
                "Já existe uma linha de pesos ativa para esta resolução e grupo de curso")),

        new(PesoAreaEnemErrorCodes.PesoNegativo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.peso_negativo",
                "Peso de área não pode ser negativo")),

        new(PesoAreaEnemErrorCodes.PesosSomamZero,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.pesos_somam_zero",
                "Os pesos das áreas não podem somar zero")),

        new(PesoAreaEnemErrorCodes.PesoExcedeMaximo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.peso_excede_maximo",
                "Peso de área excede o teto persistível")),

        new(PesoAreaEnemErrorCodes.AreaForaDoDominio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.area_fora_do_dominio",
                "Área fora das áreas do Peso por Área")),

        new(PesoAreaEnemErrorCodes.AreaRepetida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.area_repetida",
                "Área informada mais de uma vez")),

        new(PesoAreaEnemErrorCodes.AreaFaltando,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.area_faltando",
                "Falta o peso de alguma das cinco áreas")),

        new(PesoAreaEnemErrorCodes.AreasEmExcesso,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.areas_em_excesso",
                "Lista de áreas com itens demais")),

        new(PesoAreaEnemErrorCodes.CorteNegativo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.corte_negativo",
                "Corte de área não pode ser negativo")),

        new(PesoAreaEnemErrorCodes.CorteExcedeMaximo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.corte_excede_maximo",
                "Corte de área excede a nota máxima de uma área do ENEM")),

        new(PesoAreaEnemErrorCodes.BaseLegalObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.base_legal_obrigatoria",
                "Base legal é obrigatória")),

        new(PesoAreaEnemErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.base_legal_tamanho",
                "Tamanho da base legal inválido")),

        new(PesoAreaEnemErrorCodes.BaseLegalCaractereInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.peso_area_enem.base_legal_caractere_invalido",
                "A base legal contém o caractere nulo ou caractere que não é texto")),

        new(PesoAreaEnemErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.peso_area_enem.nao_encontrado",
                "Linha de pesos do ENEM não encontrada")),
    ];
}
