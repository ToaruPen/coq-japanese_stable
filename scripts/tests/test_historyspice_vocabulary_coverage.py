from __future__ import annotations

import json
from pathlib import Path

from scripts import historyspice_vocabulary_coverage as coverage

_REPO_ROOT = Path(__file__).resolve().parents[2]
_ITEM_BLESSING_LEAVES = {
    "blessing",
    "gift",
    "joy",
    "victory",
    "rapture",
    "charm",
    "bliss",
    "pride",
    "wonder",
    "ecstasy",
    "jewel",
    "prize",
    "mirth",
    "solace",
    "triumph",
    "beauty",
    "grace",
    "glamor",
    "paragon",
    "dream",
    "lure",
    "promise",
    "boon",
    "nursling",
    "mite",
    "sprout",
    "urchin",
    "boy",
    "girl",
    "friend",
    "cohort",
    "cousin",
    "brother",
    "sister",
    "mother",
    "father",
    "comrade",
    "lover",
    "flame",
    "suitor",
    "foe",
    "rival",
    "star",
    "sun",
    "moon",
    "son",
    "daughter",
    "dear",
    "beloved",
    "pet",
    "flower",
}
_GOSSIP_LEADIN_LEAVES = {
    "Did you hear?",
    "I heard that",
    "Someone told me that",
    "Rumor is that",
    "Bird chatter says that",
}
_EXTRADIMENSIONAL_REALM_VOID_CULT_FORM_LEAVES = {
    "realm",
    "domain",
    "dominion",
    "orbit",
    "plane",
    "expanse",
    "unit",
    "quantity",
    "radius",
    "degree",
    "stratum",
    "nether",
    "vacuum",
    "nihility",
    "abyss",
    "chasm",
    "fissure",
    "gulf",
    "gap",
    "lacuna",
    "womb",
    "nothingness",
    "schism",
    "vacuous",
    "vacant",
    "hollow",
    "pale",
    "spotless",
    "blank",
    "prosaic",
    "dreary",
    "dead",
    "flat",
    "inert",
    "forsaken",
    "vast",
    "infinite",
    "Charming *cult*",
    "*cult* and Love",
    "Verdant *cult*",
    "*cult*, Aspect of Plants",
    "All-Seeing *cult*",
    "*cult* and the Eye",
    "Entropic *cult*",
    "*cult* and Entropy",
    "Frigid *cult*",
    "*cult*, Aspect of Winter",
    "Tyrannical *cult*",
    "Astral *cult*",
    "*cult* and Astral Projection",
    "Hidden *cult*",
    "Lost *cult*",
    "Disjointed *cult*",
    "*cult*, the Divided",
    "Shining *cult*",
    "*cult* and Light",
    "Unified *cult*",
    "*cult*, Aspect of Oneness",
    "Two-Faced *cult*",
    "*cult* and the Parallel",
    "*cult* and the Before",
    "Oracular *cult*",
    "*cult*, Chrome and Brass",
    "*cult*, the Circuit-Maze",
    "Fevered *cult*",
    "*cult*, Aspect of Fire",
    "traveling *cult*",
    "cosmic *cult*",
    "*cult*, the Door",
    "violent *cult*",
    "*cult*, Aspect of Inertia",
    "cerebral *cult*",
    "*cult* and the Mind",
    "vampiric *cult*",
    "*cult*, the Succubus",
    "*cult*, the Mover",
    "Long-Arm *cult*",
    "*cult*, the Here and There",
    "entangled *cult*",
    "fickle *cult",
    "whimsical *cult*",
    "*cult*, Now and Then",
    "Immortal *cult*",
    "*cult*, the Many",
    "popular *cult*",
}
_JEWELS_ELEMENT_LEAVES = {
    "jeweler",
    "geologist",
    "jewels",
    "crystal",
    "ruby",
    "sapphire",
    "emerald",
    "agate",
    "jasper",
    "gemstones",
    "peridot",
    "jeweled",
    "shining",
    "radiant",
    "lustrous",
    "lucent",
    "glittering",
    "jewel",
    "jasper stone",
    "gemstone",
    "amethyst",
    "rubies",
    "sapphires",
    "emeralds",
    "agates",
    "jasper stones",
    "amethysts",
    "peridots",
    "adorning one's self with jewels",
    "setting a jewel into <spice.jewelry.!random.article>",
    "mining <^.nounsPlural.!random>",
    "hoarding <^.nounsPlural.!random>",
    "by trapping <spice.pronouns.object.!random> in <^.nouns.!random.article>",
    "with a dagger made of <^.materials.!random>",
    "came across a trove of <^.adjectives.!random> <^.nounsPlural.!random>",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "looting all the jewels from the homes",
    "bristling with carnivorous geodes",
    "man-eating geodes",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "<spice.commonPhrases.epic.!random.article> *var* was discovered",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "adorned with <^.nounsPlural.!random> and <^.nounsPlural.!random>",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "bathed in *var* light",
}
_CHANCE_ELEMENT_LEAVES = {
    "gambler",
    "chance itself",
    "copper",
    "luck",
    "fate",
    "silver",
    "gold",
    "platinum",
    "lucky",
    "random",
    "arbitrary",
    "fateful",
    "fortuitous",
    "charmed",
    "weighted",
    "cursed",
    "blessed",
    "<spice.numbers.!random>-sided die",
    "coin",
    "good omen",
    "bad omen",
    "token",
    "charm",
    "vessel of the Fates",
    "<spice.numbers.!random>-sided dice",
    "coins",
    "good omens",
    "bad omens",
    "tokens",
    "charms",
    "vessels of the Fates",
    "rolling <spice.numbers.!random>-sided dice",
    "flipping coins",
    "determining outcomes by chance",
    "throwing bones to portend the future",
    "interpreting omens from the observed flight of birds",
    "with <^.nouns.!random.article> made of <^.materials.!random>",
    "by flipping a coin and agreeing to kill either <spice.pronouns.object.!random> "
    "or <entity.objectPronoun>self based on the outcome",
    "with a revolver loaded with a single bullet",
    "fell into a crevasse and survived against all odds",
    "watched a flipped coin land on heads ten times in a row",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "rolling dice to determine which ones to destroy from the homes",
    "rife with bad omens",
    "rotten luck",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a *var* was found in every home",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "after choosing a spouse at random",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "after auguring a *var* omen",
}
_CIRCUITRY_ELEMENT_LEAVES = {
    "tinker",
    "electrician",
    "wire",
    "copper",
    "silicon",
    "circuits",
    "electric current",
    "logic",
    "diodes",
    "transistors",
    "capacitors",
    "coils",
    "vacuum tubes",
    "analog",
    "digital",
    "logical",
    "voltaic",
    "computerized",
    "electric",
    "wired",
    "copper-plated",
    "coiled",
    "circuitboard",
    "circuit",
    "logic gate",
    "light bulb",
    "battery",
    "vacuum tube",
    "oscilloscope",
    "circuitboards",
    "logic gates",
    "light bulbs",
    "batteries",
    "oscilloscopes",
    "wiring <spice.elements.!random.nounsPlural.!random> to <spice.elements.!random.nounsPlural.!random>",
    "with <^.nouns.!random.article> made of <^.materials.!random>",
    "with malicious soldering",
    "by wiring <spice.pronouns.object.!random> to hundreds of <^.nounsPlural.!random>",
    "by dividing by zero",
    "met a computerized version of <entity.objectPronoun>self",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "soldering together the children",
    "rife with electric arcs",
    "voltaic lunes",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a *var* lit up in every home",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "while telecommuting",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "inside a *var* machine",
}
_GLASS_ELEMENT_LEAVES = {
    "glassblower",
    "window maker",
    "glass",
    "sand",
    "glazed",
    "clear",
    "prismatic",
    "prism",
    "mirror",
    "prisms",
    "mirrors",
    "staring into mirrors",
    "staining glass",
    "glassblowing",
    "burying prisms under the earth",
    "by trapping <spice.pronouns.object.!random> in a prism",
    "with a dagger made of <^.materials.!random>",
    "saw <entity.possessivePronoun> own reflection in a river",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "transparent visage",
    "sandy hair",
    "mirrored eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "shattering all the glass in the homes",
    "devastated by torrential glass storms",
    "glass-swept knolls",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a *var* shattered in every home",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "in a cathedral of stained glass",
    "in a hall of mirrors",
    "inside a colossal prism",
    "during a torrential glass storm",
    "curtained under a *var* lune",
}
_ICE_ELEMENT_LEAVES = {
    "geologist",
    "stargazer",
    "astronomer",
    "glassblower",
    "winter eremite",
    "ice",
    "snow",
    "frost",
    "rime",
    "frigid",
    "frosty",
    "icy",
    "freezing",
    "glacial",
    "wintry",
    "block of ice",
    "miniature glacier",
    "snowflake",
    "icicle",
    "blocks of ice",
    "miniature glaciers",
    "snowflakes",
    "icicles",
    "burying one's self to the neck in snow",
    "ice sculpting",
    "encasing things in ice",
    "taking a spiritual trek through the tundra",
    "growing thicker skin from living out in the snow",
    "by trapping <spice.pronouns.object.!random> in <^.nouns.!random.article>",
    "with a dagger made of <^.materials.!random>",
    "by allowing <spice.pronouns.object.!random> to continue living in a universe where heat death is inevitable",
    "made a solitary trek through a lifeless tundra",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "stealing oil from the heat lamps in the villages",
    "devastated by icy winds",
    "scathing gales",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "snow fell everywhere",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "while stark naked in freezing temperatures",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "in the thin and *var* air",
}
_MIGHT_ELEMENT_LEAVES = {
    "soldier",
    "gladiator",
    "zetachrome",
    "crysteel",
    "iron",
    "bone",
    "mighty",
    "colossal",
    "dominant",
    "potent",
    "commanding",
    "sword",
    "mace",
    "axe",
    "iron gauntlet",
    "skull",
    "hammer",
    "pummel",
    "helmet",
    "breastplate",
    "gauntlet",
    "boot",
    "swords",
    "guns",
    "maces",
    "axes",
    "iron gauntlets",
    "skulls",
    "hammers",
    "pummels",
    "bones",
    "helmets",
    "breastplates",
    "gauntlets",
    "boots",
    "smashing things to bits",
    "cleaving skulls",
    "making demands",
    "imposing one's will",
    "hearing the lamentations of one's foes",
    "weighing items on a scale and then smashing the scale after not liking the results",
    "with <^.nouns.!random.article> made of <^.materials.!random>",
    "by choking the life out of <spice.pronouns.object.!random>",
    "by slaughtering <spice.pronouns.possessive.!random> entire clan",
    "got into a tavern brawl",
    "trekked across a field of skulls and bones",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "razing to the ground the homes",
    "particularly rejoicing in the lamentations",
    "rife with smashed rubble",
    "trash heaps",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a great battle was won",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "after murdering a foe <^.murdermethods.!random>",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "looming with *var* presence",
}
_SALT_ELEMENT_LEAVES = {
    "cook",
    "salt",
    "briny water",
    "briny",
    "salt-spangled",
    "desiccated",
    "pinch of salt",
    "dram of brine",
    "spice root",
    "leaf",
    "brine",
    "spices",
    "leaves",
    "seasoning food",
    "water fasting",
    "by swilling the moisture from his skin",
    "by pouring salt into his eyes",
    "by poisoning <spice.pronouns.possessive.!random> water",
    "trekked through a lifeless salt pan and stumbled on a mysterious monolith",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "sowing with salt the fields",
    "evaporated of all moisture",
    "bone-dry desert",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "the taste of *var* filled everyone's mouth",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "after abstaining from drinking water for <spice.instancesOf.twoToTen.!random> days",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "upon chewing the *var* leaf",
}
_SCHOLARSHIP_ELEMENT_LEAVES = {
    "scholar",
    "scribe",
    "philosopher",
    "scientist",
    "historian",
    "clockwork tools",
    "data disks",
    "philosophical",
    "shrewd",
    "inquisitive",
    "quill",
    "inkwell",
    "scroll",
    "quills",
    "inkwells",
    "scrolls",
    "contemplating the meaning of things",
    "taking measurements",
    "by filling him with existential despair",
    "by inventing and releasing clockwork beetles",
    "by writing him out of the annals of history",
    "contemplated the nature of things in solitude",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "kidnapping the smartest children from the homes",
    "rife with burnt books and corroded data disks",
    "data corruption",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a college was founded",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "while reciting <entity.possessivePronoun> favorite treatise",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "with *var* eyes peeled",
}
_STARS_ELEMENT_LEAVES = {
    "astrologist",
    "astronomer",
    "stargazer",
    "stardust",
    "meteorite",
    "motes of light",
    "starlight",
    "shining",
    "radiant",
    "luminous",
    "gleaming",
    "white-hot",
    "mote of light",
    "star in a bottle",
    "telescope",
    "meteorites",
    "stars in a bottle",
    "telescopes",
    "staring at the night sky",
    "worshipping the stars",
    "sketching the constellations",
    "hoarding <^.nounsPlural.!random>",
    "by trapping <spice.pronouns.object.!random> in <^.nouns.!random.article>",
    "with a dagger made of <^.materials.!random>",
    "by forecasting <spice.pronouns.possessive.!random> doom",
    "spotted an unidentified object in the night sky",
    "witnessed a falling star",
    "came across a trove of <^.adjectives.!random> <^.nounsPlural.!random>",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "raining meteors down from the sky onto the settlements",
    "devastated by smoldering stardust",
    "fuming stardew",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "the stars aligned to form a constellation in the shape of a *var*",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "while reciting the properties of <entity.possessivePronoun> favorite star",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "bathed in *var* light",
}
_TIME_ELEMENT_LEAVES = {
    "historian",
    "scribe",
    "tiny gears",
    "time itself",
    "the fabric of time",
    "perpetual",
    "boundless",
    "periodic",
    "rhythmic",
    "orbital",
    "miniature clock",
    "moment in time chosen arbitrarily",
    "hourglass",
    "atomic clock",
    "miniature clocks",
    "forgotten seconds of one's life",
    "hourglasses",
    "atomic clocks",
    "watching sand sift through an hourglass",
    "worshipping the past, present, and future",
    "stopping time",
    "hoarding <^.nounsPlural.!random>",
    "by trapping <spice.pronouns.object.!random> in <^.nouns.!random.article>",
    "with a dagger made of <^.materials.!random>",
    "by letting entropy feast on <spice.pronouns.possessive.!random> flesh",
    "fell into a well and lived a hundred lives",
    "came upon a river flowing backwards",
    "came across a trove of <^.adjectives.!random> <^.nounsPlural.!random>",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "conducting midnight raids on the villages",
    "rife with stray portals to other places and times",
    "fraying reality-edges",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a *var* stopped working in every home",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "in a ceremony that lasted a full year",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "with graceful, *var* movement",
}
_TRAVEL_ELEMENT_LEAVES = {
    "explorer",
    "nomad",
    "water",
    "farewells",
    "brick",
    "leather",
    "wood",
    "silk",
    "paper",
    "rope",
    "wind",
    "canvas",
    "campfire ashes",
    "foreign",
    "unfamiliar",
    "strange",
    "timeworn",
    "worldly",
    "traveling",
    "wandering",
    "otherworldly",
    "compass",
    "altimeter",
    "astrolabe",
    "map",
    "sextant",
    "bedroll",
    "journal",
    "boat",
    "pair of sandals",
    "compasses",
    "altimeters",
    "astrolabes",
    "maps",
    "sextants",
    "bedrolls",
    "journals",
    "boats",
    "pairs of sandals",
    "wandering without purpose",
    "making a pilgrimage",
    "climbing a mountain",
    "skirring the stars",
    "leaving <entity.possessivePronoun> home forever",
    "setting out on an adventure to find a famous <spice.elements.!random.nouns.!random>",
    "with <^.nouns.!random.article> made of <^.materials.!random>",
    "by pursuing <spice.pronouns.object.!random> to exhaustion",
    "by cooking <spice.pronouns.object.!random> for sustenance",
    "broke bread with a pilgrim",
    "journeyed to a sacred shrine",
    "had a dream that <entity.subjectPronoun> was <^.practices.!random>",
    "<^.adjectives.!random> eyes",
    "reputation for murdering someone <^.murdermethods.!random>",
    "<^.adjectives.!random> visage",
    "with its mouth full of <^.materials.!random>",
    "with <^.nounsPlural.!random> on its eyes",
    "with <^.adjectives.!random.article> <^.nouns.!random> in each hand",
    "forcibly relocating the families",
    "vacant of signs of life",
    "emptiness",
    "<spice.history.regions.organizingPrinciple.religion.generic.thingsTheyDid.!random>",
    "a new planet was discovered",
    "a famous <^.professions.!random> completed <spice.pronouns.possessive.!random> work on a legendary *var*",
    "upon the completion of a great pilgrimage",
    "inside a colossal <^.nouns.!random>",
    "in a cathedral ornamented with <^.nounsPlural.!random>",
    "Waist deep in a lake of <^.materials.!random>",
    "in view of odd and *var* sands",
}
_COMMON_PHRASES_COOKING_RECIPES_LEAVES = {
    "cooking",
    "baking",
    "brewing",
    "roasting",
    "stewing",
    "searing",
    "braising",
    "boiling",
    "pickling",
    "fermenting",
    "frying",
    "broiling",
    "steaming",
    "grilling",
    "recipes",
    "dishes",
    "meals",
    "food",
    "vittle",
    "snacks",
    "cuisine",
    "chow",
    "grub",
    "mess",
    "victual",
    "fare",
    "courses",
    "eats",
    "servings",
}
_COMMON_PHRASES_LANDSCAPE_LEAVES = {
    "sea",
    "ocean",
    "lake",
    "lagoon",
    "pond",
    "cistern",
    "brine",
    "surf",
    "wastes",
    "ruin",
    "decay",
    "havoc",
    "barren",
    "flats",
    "badland",
    "dunes",
    "wreck",
    "bog",
    "marsh",
    "moor",
    "void",
    "quagmire",
    "shire",
    "end",
    "hedge",
    "furrow",
    "hearth",
    "hold",
    "reach",
}
_COMMON_PHRASES_ANNALS_STATUS_LEAVES = {
    "ascended to the throne",
    "took the crown",
    "ascended to the crown",
    "proclaimed <entity.objectPronoun>self sultan",
    "took the reins of power",
    "seized the crown",
    "seized the gilded scepter",
    "killed",
    "murdered",
    "drawn and quartered",
    "exiled",
    "imprisoned",
    "launched into orbit",
    "buried deep under the earth",
    "trapped in <spice.elements.entity$elements[random].nouns.!random.article>",
    "tried but pardoned",
    "great",
    "terrible",
    "ordained",
    "destined",
    "foretold",
    "eminent",
    "glorious",
    "famed",
    "honored",
    "venerable",
    "marriage",
    "union",
    "join",
    "wed",
    "matrimony",
    "nuptial",
    "wedlock",
    "betrothed",
    "love",
    "delight",
    "devotion",
    "celebrated",
    "lauded",
    "praised",
    "consecrated",
    "eulogized",
    "revered",
    "extolled",
    "reputable",
    "esteemed",
    "prominent",
    "renowned",
    "notable",
}
_COMMON_PHRASES_TRANSFER_RECOVERY_LEAVES = {
    "bequeathed",
    "gave",
    "bestowed",
    "conferred",
    "entrusted",
    "granted",
    "retrieve",
    "find",
    "recover",
    "fetch",
    "salvage",
    "acquire",
    "procure",
    "snag",
    "restore",
    "rehabilitate",
    "rescue",
    "revive",
    "redeem",
    "rejuvenate",
    "win back",
}
_COMMON_PHRASES_SENTIMENT_DESCRIPTOR_LEAVES = {
    "abhor",
    "detest",
    "denounce",
    "scorn",
    "dishonor",
    "blessing",
    "honor",
    "favor",
    "boon",
    "comfort",
    "gift",
    "bygone",
    "erstwhile",
    "once",
    "old",
    "calmly",
    "gently",
    "quietly",
}
_COMMON_PHRASES_CELEBRATION_LEAVES = {
    "celebrate",
    "remember",
    "observe",
    "extol",
    "rejoice in",
    "cried out in joy",
    "drank themselves into stupors",
    "told stories and renewed friendships",
    "celebration",
    "joy",
    "jubilee",
    "gaiety",
    "jubilation",
}
_COMMON_PHRASES_CONFLICT_COALITION_LEAVES = {
    "challenge",
    "provoke",
    "aggrieve",
    "chastisement",
    "chastening",
    "rebuking",
    "<^.curse.!random>",
    "coalition",
    "alliance",
    "confederacy",
    "league",
    "conspiracy",
    "federation",
    "union",
    "party",
}
_COMMON_PHRASES_CREATION_GATHERING_CONQUEST_LEAVES = {
    "composed",
    "invented",
    "fashioned",
    "imagined",
    "consecrated",
    "congregated",
    "gathered",
    "flocked together",
    "massed",
    "conquered",
    "annexed",
    "discovered",
    "subjugated",
}
_COMMON_PHRASES_CORRUPTION_CORONATION_LEAVES = {
    "corrupt",
    "fraudulent",
    "venal",
    "debauched",
    "base",
    "perfidious",
    "knavish",
    "treacherous",
    "crowned",
    "declared",
    "proclaimed",
}
_COMMON_PHRASES_DEFIANCE_DEMONSTRATION_LEAVES = {
    "defied",
    "flouted",
    "mocked",
    "spurned",
    "thwarted",
    "scorned",
    "eluded",
    "demonstrate",
    "exhibit",
    "prove",
    "display",
    "evince",
}
_COMMON_PHRASES_DEPRAVITY_DESPOTS_LEAVES = {
    "depravity",
    "degeneracy",
    "decay",
    "wickedness",
    "perversion",
    "despots",
    "lords",
    "aristocrats",
    "leaders",
    "magistrates",
    "shepherds",
}
_COMMON_PHRASES_DOOR_EMERGENCE_LEAVES = {
    "door",
    "portal",
    "entryway",
    "egress",
    "gate",
    "hatch",
    "embraced",
    "accepted",
    "adopted",
    "forgotten",
    "emerged",
    "materialized",
    "sprang forth",
}
_COMMON_PHRASES_ENACTING_ENTWINED_EPIC_LEAVES = {
    "enacting",
    "setting into motion",
    "putting into place",
    "entropist",
    "entwined",
    "braided",
    "embracing",
    "beautiful",
    "unsurpassed",
    "bewitching",
    "sublime",
}
_COMMON_PHRASES_TIME_FAMILY_FESTIVAL_FIND_LEAVES = {
    "eternally",
    "forever",
    "always",
    "family",
    "kith",
    "clan",
    "brood",
    "kinfolk",
    "children",
    "folk",
    "tribe",
    "progeny",
    "fate",
    "fortune",
    "festival",
    "feast",
    "carnival",
    "jubilee",
    "holiday",
    "find",
    "locate",
    "pinpoint",
}
_COMMON_PHRASES_FINESSE_FOES_LEAVES = {
    "finesse",
    "agility",
    "skill",
    "artfulness",
    "artistry",
    "dexterity",
    "prowess",
    "deftness",
    "foes",
    "enemies",
}
_COMMON_PHRASES_PEOPLE_TIME_GHOST_LEAVES = {
    "folks",
    "people",
    "beings",
    "forever",
    "for all time",
    "now and forever",
    "in perpetuity",
    "from then on",
    "from that day forth",
    "for the rest of <entity.name>'s life",
    "ghost",
    "shade",
    "spectre",
    "devil",
    "wraith",
}
_COMMON_PHRASES_GIFT_GRAVE_GREATLY_LEAVES = {
    "gift",
    "favor",
    "grant",
    "dower",
    "boon",
    "deep",
    "grave",
    "joyous",
    "heartfelt",
    "greatly",
    "mightily",
    "much",
    "immensely",
}
_COMMON_PHRASES_GROUP_HARK_HARM_LEAVES = {
    "group",
    "sect",
    "organization",
    "party",
    "cabal",
    "group of friends",
    "group of lovers",
    "hark",
    "attend",
    "attention",
    "pay heed",
    "<^.adventurer.!random>",
    "harm",
    "abuse",
    "undermine",
    "wrong",
}
_COMMON_PHRASES_HEARTH_HELPING_HISTORIC_LEAVES = {
    "hearth",
    "home",
    "hearthstone",
    "haunt",
    "seat",
    "roost",
    "helping",
    "assisting",
    "aiding",
    "historic",
    "celebrated",
    "influential",
    "illustrious",
    "imperial",
}
_COMMON_PHRASES_HOLD_HONORING_HORROR_LEAVES = {
    "hold",
    "hide",
    "contain",
    "honoring",
    "defending",
    "loving",
    "horror",
    "abomination",
    "shame",
    "anathema",
    "atrocity",
}
_COMMON_PHRASES_HUMBLE_HUNTER_IMPORTANCE_LEAVES = {
    "humble",
    "quiet",
    "modest",
    "gentle",
    "stalker",
    "assassin",
    "importance",
    "value",
    "significance",
    "<^.sanctity.!random>",
}
_COMMON_PHRASES_IN_HONOR_OF_INAUGURATION_INSPIRED_LEAVES = {
    "in honor of the <^.occasion.!random>",
    "to honor the <^.occasion.!random>",
    "to show their appreciation",
    "in celebration of the <^.occasion.!random>",
    "to celebrate the <^.occasion.!random>",
    "inauguration",
    "opening",
    "founding",
    "inspired",
    "roused",
    "stirred",
}
_COMMON_PHRASES_INTERESTING_INTREPID_LEAVES = {
    "interesting",
    "intriguing",
    "delightful",
    "fascinating",
    "intrepid",
    "bold",
    "courageous",
    "gallant",
    "lionhearted",
    "valiant",
}
_COMMON_PHRASES_DISCOVERY_KIND_LEAVES = {
    "it was discovered",
    "the people learned",
    "the people of Qud learned",
    "generous",
    "gracious",
    "compassionate",
    "gentle",
    "courteous",
    "lenient",
    "cordial",
}
_COMMON_PHRASES_LARVAE_LAWS_LEAVES = {
    "fry",
    "larvae",
    "grub",
    "maggots",
    "worms",
    "laws",
    "doctrine",
    "statutes",
    "edicts",
    "ordinances",
    "injunctions",
}
_COMMON_PHRASES_LEARNED_LEARNED_OF_LEAVES = {
    "learned of",
    "learned about",
    "discovered",
    "found out about",
    "came upon",
    "learned",
    "determined",
    "found out",
    "ascertained",
    "gathered",
}
_COMMON_PHRASES_LEARNING_LISTEN_LEAVES = {
    "learning",
    "discovering",
    "hearing of",
    "becoming acquainted with",
    "listen",
    "hark",
    "hear me",
    "mind what I say",
}
_COMMON_PHRASES_LIBERATED_LEAVES = {
    "liberated",
    "freed",
    "rescued",
}
_COMMON_PHRASES_LOST_LEAVES = {
    "in a game of dice",
    "to a local thief",
    "to a local pickpocket",
    "in a foolhardy bet",
    "lost",
    "vanished",
    "moldered",
    "desolate",
    "extinct",
}
_COMMON_PHRASES_LOVE_LOVERS_LEAVES = {
    "love",
    "revere",
    "honor",
    "worship",
    "cherish",
    "venerate",
    "esteem",
    "treasure",
    "pay homage to",
    "lovers",
    "<^.betrothed.!random>",
}
_COMMON_PHRASES_LUCKILY_MARVEL_LEAVES = {
    "luckily",
    "by the grace of fate",
    "fortuitously",
    "by chance",
    "marvel",
    "stare",
    "be awed",
    "stand in awe",
}
_COMMON_PHRASES_MIGHT_MISUSE_MORALITY_LEAVES = {
    "might",
    "power",
    "prowess",
    "misuse",
    "abuse",
    "perversion",
    "morality",
    "decency",
    "virtue",
    "chastity",
    "godliness",
    "principles",
    "the moral code",
}
_COMMON_PHRASES_MUG_NOBLE_LEAVES = {
    "mug",
    "stein",
    "skin",
    "bladder",
    "ewer",
    "jug",
    "canteen",
    "glass",
    "cup",
    "noble",
    "virtuous",
    "honorable",
}
_COMMON_PHRASES_OBJECT_OBSERVE_OCCASION_LEAVES = {
    "object",
    "observe",
    "mark",
    "regard",
    "behold",
    "read",
    "occasion",
    "ceremony",
    "affair",
    "union",
}
_COMMON_PHRASES_ODIOUS_LEAVES = {
    "odious",
    "wicked",
    "devilish",
    "villainous",
    "abominable",
    "degenerate",
    "fiendish",
    "foul",
    "nefarious",
}
_COMMON_PHRASES_ONLOOKER_PICKS_LEAVES = {
    "watcher",
    "beholder",
    "onlooker",
    "witness",
    "picks",
    "culls",
    "winnows",
    "plucks",
}
_COMMON_PHRASES_PIGFARM_PLAGUE_PLAN_LEAVES = {
    "<^.farm.!random>",
    "ranch",
    "pasture",
    "plague",
    "curse",
    "vex",
    "plan",
    "scheme",
    "idea",
    "stratagem",
    "ploy",
    "ruse",
}
_COMMON_PHRASES_PRACTICE_PRETENDER_PRIZED_LEAVES = {
    "practice",
    "art",
    "pretender",
    "claimant",
    "aspirant",
    "prized",
    "precious",
    "cherished",
    "treasured",
}
_COMMON_PHRASES_PROFANITY_PROHIBITED_LEAVES = {
    "profanity",
    "obscenity",
    "blasphemy",
    "impiety",
    "irreverence",
    "banned",
    "prohibited",
    "outlawed",
}
_COMMON_PHRASES_PROTECT_PROTECTION_LEAVES = {
    "protect",
    "defend",
    "safeguard",
    "keep safe",
    "preserve",
    "protection",
    "support",
    "encouragement",
    "furtherance",
    "patronage",
}
_COMMON_PHRASES_PUFF_RAVAGED_LEAVES = {
    "puff",
    "wisp",
    "noseful",
    "sniff",
    "ravaged",
    "rampaged through",
    "pillaged",
    "plundered",
    "wreaked havoc on",
    "laid waste to",
}
_COMMON_PHRASES_REMEMBER_RIFE_RITUALS_LEAVES = {
    "remember",
    "recall",
    "rife",
    "rampant",
    "prevalent",
    "reigning",
    "widespread",
    "rituals",
    "rites",
    "rites of passage",
    "customs",
    "practices",
    "ceremonies",
}
_COMMON_PHRASES_CONFLICT_RESCUE_VICTORY_LEAVES = {
    "rogue",
    "nefarious",
    "bandit",
    "trickster",
    "criminal",
    "sacked",
    "destroyed",
    "pillaged",
    "ravaged",
    "burned down",
    "savior",
    "liberator",
    "defender",
    "scourge",
    "terror",
    "bane",
    "pest",
    "woe",
    "sorrow",
    "slaughtered",
    "persecuted",
    "vanquished",
    "conquered",
    "routed",
    "subdued",
    "triumphed over",
}
_COMMON_PHRASES_DESCRIPTOR_EMOTION_WARNING_LEAVES = {
    "strange",
    "weird",
    "curious",
    "rare",
    "marvelous",
    "uncanny",
    "suspiciously",
    "tentatively",
    "warily",
    "carefully",
    "anxiously",
    "tamed",
    "pacified",
    "subdued",
    "gentled",
    "brought to heel",
    "thankful",
    "grateful",
    "much obliged",
    "warning",
    "lesson",
    "admonition",
    "example",
    "forewarning",
    "wild",
    "untamed",
    "feral",
    "savage",
    "barbaric",
    "woe",
    "misery",
    "gloom",
    "anguish",
    "agony",
    "sorrow",
    "shame",
    "torment",
    "wonder",
    "astonishment",
    "awe",
    "reverence",
}
_COMMON_PHRASES_CIVIC_SOCIAL_WORK_LEAVES = {
    "secluded",
    "quiet",
    "small",
    "remote",
    "services",
    "service",
    "assistance",
    "work",
    "labor",
    "society",
    "culture",
    "civic life",
    "social order",
    "spouse",
    "partner",
    "companion",
    "mate",
    "task",
    "errand",
    "job",
    "project",
    "charge",
    "stint",
}
_COMMON_PHRASES_VALUE_DIPLOMACY_SUPPORT_LEAVES = {
    "treasures",
    "secrets",
    "riches",
    "pearls",
    "mysteries",
    "treating",
    "striking a deal",
    "conferring",
    "supports",
    "protects",
    "promotes",
    "champions",
    "cheers",
}
_COMMON_PHRASES_PLACE_TIME_LEAVES = {
    "home",
    "holme",
    "<^.farm.!random>",
    "orchard",
    "grove",
    "yard",
    "fold",
    " quadrangle",
    "square",
    "quad",
    "years ago",
    "beyond the gulf of time",
    "back when the musa was perpetually ripe",
    "early in the days after the reign of Resheph",
    "long ago",
}
_COMMON_PHRASES_DISCOVERY_LINEAGE_MARKING_CUSTOMS_LEAVES = {
    "saw",
    "found",
    "discovered",
    "child",
    "lamb",
    "heir",
    "scion",
    "seed",
    "kin",
    "babe",
    "heiress",
    "progeny",
    "sprout",
    "stamped",
    "painted",
    "engraved",
    "embossed",
    "etched",
    "adorned",
    "carved",
    "ways",
    "customs",
    "culture",
    "habits",
    "priorities",
    "manner",
}
_COMMON_PHRASES_SETTLEMENT_TRAVEL_VOICE_LEAVES = {
    "settle",
    "visit",
    "dwell",
    "lodge",
    "take root",
    "take up residence",
    "settled",
    "roosted",
    "lodged",
    "resided",
    "took up residence",
    "traveling",
    "visiting",
    "roaming",
    "trek",
    "journey",
    "travel",
    "go",
    "hike",
    "wander",
    "trekked",
    "journeyed",
    "traveled",
    "voyaged",
    "while traveling",
    "as <entity.subjectPronoun> rode",
    "during a trek",
    "during an expedition",
    "while on an expedition",
    "while on a trek",
    "visited",
    "<^.trekked.!random> to",
    "stretches",
    "voice",
    "utter",
    "say",
    "sound",
}
_INSTANCES_ABDICATION_PROTOCOL_APPROACH_LEAVES = {
    "abdicate the throne",
    "take an extended sabbatical",
    "step down",
    "abdicated the throne",
    "died under mysterious circumstances",
    "disappeared",
    "was assassinated",
    "above all else",
    "come what may",
    "as long as you are respectful",
    "per our custom",
    "after several tumultuous years",
    "approach",
    "meet",
    "begin",
    "match",
    "come at",
    "surround",
    "threaten",
    "accost",
}
_INSTANCES_BLESS_MAIMING_FAITH_LEAVES = {
    "bless",
    "thank",
    "exalt",
    "give thanks for",
    "praise",
    "honor",
    "maimed",
    "dismembered",
    "crushed",
    "flattened",
    "severed",
    "punctured",
    "broke faith with",
    "betrayed",
    "committed treason against",
    "broke trust with",
    "deceived",
}
_INSTANCES_SPEECH_COMMON_FOLK_CURSE_LEAVES = {
    "chanted",
    "sang",
    "shouted",
    "crooned",
    "yodeled",
    "roared",
    "come, close!",
    "commoners",
    "common folk",
    "plebians",
    "rabble",
    "herd",
    "masses",
    "cried out",
    "bellowed",
    "curse",
    "a blight upon",
    "a curse upon",
}
_INSTANCES_DEADLY_LIQUIDS_LEAVES = {
    "lava",
    "acid",
    "neutron flux",
    "black ooze",
    "green goo",
    "brown sludge",
    "asphalt",
    "molten wax",
}
_INSTANCES_DEAR_ONES_DESIRE_LEAVES = {
    "friends",
    "lovers",
    "children",
    "cohorts",
    "comrades",
    "desire",
    "want",
    "need",
    "covet",
    "require",
    "yearn for",
    "am in need of",
    "have use for",
    "must have",
    "must get a hold of",
}
_INSTANCES_SOCIAL_FATE_MOVEMENT_LEAVES = {
    "thrown off a cliff",
    "humiliated at a banquet",
    "launched into orbit",
    "with the clever use of a lifelike hologram",
    "dwell",
    "live",
    "work",
    "toil",
    "labor",
    "fate",
    "chance",
    "the way the musa peels",
    "misfortune",
    "flocked to",
    "gathered in droves at",
    "amassed at",
    "herded in droves to",
}
_INSTANCES_TIME_FOREST_PLACE_LEAVES = {
    "for all time",
    "for all eternity",
    "again",
    "ever again",
    "glen",
    "glade",
    "dell",
    "dale",
    "vale",
    "gorge",
    "meadow",
    "bosk",
    "grove",
    "wood",
    "weep",
    "weald",
    "root",
}
_INSTANCES_PUNISHMENT_POSSESSION_APPEAL_LEAVES = {
    "sacrificed",
    "burned at the stake",
    "buried alive",
    "drawn and quartered",
    "mummified",
    "beheaded",
    "killed after cooking a rancid meal for",
    "have",
    "acquire",
    "get a hold of",
    "obtain",
    "procure",
    "snag",
    "have you heard of",
    "are you aware of",
    "are you acquainted with",
    "have you been introduced to",
    "hold dear",
    "cherish",
    "value so highly",
    "if you would do the same",
    "if you would do it too",
    "if you would do it yourself",
}
_INSTANCES_ILLNESS_JUSTICE_KINSHIP_PROXIMITY_LEAVES = {
    "depression",
    "gout",
    "consumption",
    "brain rust",
    "ironshank",
    "scurvy",
    "brain mites",
    "leprosy",
    "existential despair",
    "justice",
    "love",
    "truth",
    "equality",
    "parity",
    "faith",
    "grace",
    "virtue",
    "honor",
    "benefience",
    "kindred",
    "sibling",
    "cousin",
    "kinsmen",
    "kinswomen",
    "kinsfolk",
    "sib",
    "brother",
    "sister",
    "leans in",
    "comes close",
    "leans forward",
    "whispers",
}
_INSTANCES_AFFIRMATION_LIFESAVE_MURDER_TIME_LEAVES = {
    "let it always be so",
    "may that never change",
    "with cybernetic surgery",
    "with astral projection",
    "by a pact with highly entropic beings",
    "lost faith in",
    "lost interest in",
    "renounced",
    "rejected",
    "stabbed to death",
    "shanked",
    "gunned down",
    "poisoned",
    "pushed off a cliff",
    "murdered under mysterious circumstances",
    "eaten alive",
    "assassinated after disparaging",
    "cooked for sustenance",
    "chopped into small pieces",
    "shot",
    "ate alive",
    "assassinated",
    "of course",
    "naturally",
    "undoubtedly",
    "obviously",
    "indeed",
    "over time",
    "over the years",
    "as the years passed",
    "eventually",
    "in time",
}
_INSTANCES_RELIGION_PROFANITY_RECENCY_REEMERGENCE_LEAVES = {
    "mud",
    "pig",
    "snout",
    "priest",
    "heretic",
    "pontiff",
    "monk",
    "cleric",
    "pagan",
    "anchorite",
    "priestess",
    "apostate",
    "pious",
    "devout",
    "heretical",
    "godly",
    "moral",
    "saintly",
    "schismatic",
    "dissident",
    "godliness",
    "god",
    "divinity",
    "virtue",
    "piety",
    "Gjaus",
    "faith",
    "holiness",
    "profanity",
    "cruelty",
    "blasphemy",
    "filth",
    "foulness",
    "vulgarity",
    "recently",
    "just a while ago",
    "a short while ago",
    "the other day",
    "reemerged",
    "appeared anew",
    "emerged anew",
    "reappeared",
    "celebrated",
    "rejoiced at",
    "reveled at",
}
_INSTANCES_REWARD_ROYAL_SECESSION_PLACE_STEPDOWN_LEAVES = {
    "reward",
    "pay you for",
    "compensate you for",
    "divine",
    "imperial",
    "sovereign",
    "holy",
    "deific",
    "kingly",
    "queenly",
    "adopted",
    "sand",
    "salt",
    "turf",
    "loam",
    "ground",
    "soil",
    "seceded",
    "separated",
    "segregated themselves",
    "insulated themselves",
    "sequestered themselves",
    "left",
    "speak to",
    "talk to",
    "find",
    "vault",
    "crypt",
    "temple",
    "shrine",
    "sanctum",
    "reactor",
    "core",
    "fruit",
    "apple",
    "red",
    "sweet",
    "step down",
    "abdicate",
    "surrender power",
}
_INSTANCES_TAR_THANK_BODY_TREMBLE_TUTOR_NUMBER_LEAVES = {
    "tar",
    "asphalt",
    "goop",
    "resin",
    "glue",
    "thank",
    "bless",
    "praise",
    "I'm grateful to",
    "I bow down to",
    "I smile on",
    "kiss",
    "hair",
    "nail",
    "finger",
    "skin",
    "fear",
    "quiver at",
    "tremble before",
    "dread",
    "shun",
    "be in awe of",
    "tutor",
    "sophist",
    "mentor",
    "lecturer",
    "scholar",
    "augur",
    "scientist",
    "philosopher",
    "sage",
    "iconoclast",
    "historian",
    "scribe",
    "wise",
    "shrewd",
    "learned",
    "erudite",
    "controversial",
    "cerebral",
    "profound",
    "methodical",
    "two",
    "three",
    "four",
    "five",
    "six",
    "seven",
    "eight",
    "nine",
    "ten",
}
_INSTANCES_UNFORTUNATE_WARRIOR_REQUEST_LEAVES = {
    "unfortunately",
    "sadly",
    "unseated",
    "ousted",
    "deposed",
    "dethroned",
    "lifted up on chairs",
    "thrown into the air joyfully",
    "venerated as idols",
    "treated to a delightful feast",
    "trade",
    "artistic",
    "monetary",
    "spiritual",
    "festive",
    "safety",
    "musical",
    "architectural",
    "technological",
    "warrior",
    "champion",
    "mercenary",
    "duelist",
    "swordfolk",
    "axefolk",
    "daggerfolk",
    "gunfolk",
    "macefolk",
    "fearsome",
    "militant",
    "knightly",
    "merciless",
    "fierce",
    "brutish",
    "death",
    "dying",
    "the void",
    "mortality",
    "battle",
    "adversaries",
    "bravery",
    "ferocity",
    "courage",
    "valor",
    "violence",
    "wardenship",
    "bloodshed",
    "war",
    "combat",
    "will you",
    "would you",
    "what do you say",
    "would like to",
    "would love to",
    "need to",
    "must",
    "ye Godless",
    "ye Heathens",
    "ye Skeptics",
    "ye Doubters",
}


def _write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")


def test_extract_historyspice_leaves_records_nested_paths() -> None:
    """Nested object and array paths are preserved in leaf records."""
    payload = {
        "spice": {
            "cooking": {
                "terrain": {
                    "salt": ["crystal", "Cool Static"],
                },
            },
        },
    }

    leaves = coverage.extract_historyspice_leaves(payload)

    assert leaves == [
        coverage.LeafRecord("spice.cooking.terrain.salt[0]", "crystal"),
        coverage.LeafRecord("spice.cooking.terrain.salt[1]", "Cool Static"),
    ]


def test_summarize_coverage_uses_exact_and_lower_ascii_keys() -> None:
    """ASCII leaf lowercase fallback counts direct key coverage."""
    leaves = [
        coverage.LeafRecord("spice.cooking.terrain.salt[0]", "cool static"),
        coverage.LeafRecord("spice.cooking.terrain.salt[1]", "Cool Static"),
        coverage.LeafRecord("spice.elements.jewels.nouns[0]", "ruby"),
    ]

    summary = coverage.summarize_coverage(leaves, {"cool static"})

    assert summary == coverage.CoverageSummary(
        unique_leaves=3,
        covered=2,
        missing=1,
        coverage_percent=66.67,
    )


def test_load_dictionary_keys_adds_ascii_lowercase_aliases(tmp_path: Path) -> None:
    """Dictionary-key loading uses the same ASCII lowercase normalization as coverage checks."""
    dictionary_path = tmp_path / "dict.ja.json"
    _write_json(dictionary_path, {"entries": [{"key": "Cool Static", "text": "冷たい静電気"}]})

    keys = coverage.load_dictionary_keys([dictionary_path])

    assert keys == {"Cool Static", "cool static"}


def test_missing_leaf_examples_are_deduplicated_and_path_ordered() -> None:
    """Missing examples preserve a representative source path for each leaf."""
    leaves = [
        coverage.LeafRecord("spice.cooking.terrain.salt[2]", "ruby"),
        coverage.LeafRecord("spice.cooking.terrain.salt[1]", "cool static"),
        coverage.LeafRecord("spice.cooking.terrain.salt[3]", "ruby"),
        coverage.LeafRecord("spice.elements.jewels.nouns[0]", "agate"),
    ]

    examples = coverage.missing_leaf_examples(leaves, {"cool static"}, limit=2)

    assert examples == [
        coverage.LeafRecord("spice.cooking.terrain.salt[2]", "ruby"),
        coverage.LeafRecord("spice.elements.jewels.nouns[0]", "agate"),
    ]


def test_build_report_splits_hse_and_all_dictionary_coverage(tmp_path: Path) -> None:
    """The report keeps HSE-specific coverage separate from all JSON dictionaries."""
    historyspice_path = tmp_path / "Base" / "HistorySpice.json"
    dictionaries_root = tmp_path / "Dictionaries"
    _write_json(
        historyspice_path,
        {
            "spice": {
                "cooking": {"terrain": {"salt": ["cool static", "ruby"]}},
                "elements": {"jewels": {"nouns": ["ruby"]}},
            },
        },
    )
    _write_json(
        dictionaries_root / "Scoped" / "historyspice-common.ja.json",
        {"entries": [{"key": "cool static", "text": "冷たい静電気"}]},
    )
    _write_json(dictionaries_root / "world-gospels.ja.json", {"entries": []})
    _write_json(dictionaries_root / "world-items.ja.json", {"entries": [{"key": "ruby", "text": "ルビー"}]})

    report = coverage.build_report(
        historyspice_path=historyspice_path,
        dictionaries_root=dictionaries_root,
        hse_dictionary_paths=[
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
        groups=("spice.cooking.*", "spice.cooking.terrain.*", "spice.elements.*"),
    )

    assert report["leaf_occurrences"] == 3
    assert report["unique_leaf_strings"] == 2
    assert report["hse_dictionary_coverage"] == {
        "unique_leaves": 2,
        "covered": 1,
        "missing": 1,
        "coverage_percent": 50.0,
    }
    assert report["all_dictionary_coverage"] == {
        "unique_leaves": 2,
        "covered": 2,
        "missing": 0,
        "coverage_percent": 100.0,
    }
    assert report["groups"] == {
        "spice.cooking.*": {
            "unique_leaves": 2,
            "covered": 2,
            "missing": 0,
            "coverage_percent": 100.0,
            "missing_examples": [],
        },
        "spice.cooking.terrain.*": {
            "unique_leaves": 2,
            "covered": 2,
            "missing": 0,
            "coverage_percent": 100.0,
            "missing_examples": [],
        },
        "spice.elements.*": {
            "unique_leaves": 1,
            "covered": 1,
            "missing": 0,
            "coverage_percent": 100.0,
            "missing_examples": [],
        },
    }


def _is_covered(leaf: str, keys: set[str]) -> bool:
    return coverage._is_covered(leaf, keys)  # noqa: SLF001


def test_historyspice_common_covers_item_blessing_component_family() -> None:
    """Historic item-name blessings are covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _ITEM_BLESSING_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_gossip_leadin_component_family() -> None:
    """Water-ritual gossip lead-ins are covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _GOSSIP_LEADIN_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_extradimensional_realm_void_cult_form_family() -> None:
    """Extradimensional realm, void, adjective, and cult-form leaves are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _EXTRADIMENSIONAL_REALM_VOID_CULT_FORM_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_jewels_element_component_family() -> None:
    """Jewels element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _JEWELS_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_chance_element_component_family() -> None:
    """Chance element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _CHANCE_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_circuitry_element_component_family() -> None:
    """Circuitry element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _CIRCUITRY_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_glass_element_component_family() -> None:
    """Glass element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _GLASS_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_ice_element_component_family() -> None:
    """Ice element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _ICE_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_might_element_component_family() -> None:
    """Might element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _MIGHT_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_salt_element_component_family() -> None:
    """Salt element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _SALT_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_scholarship_element_component_family() -> None:
    """Scholarship element vocabulary is covered except non-HSE-owned ink."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _SCHOLARSHIP_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_stars_element_component_family() -> None:
    """Stars element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _STARS_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_time_element_component_family() -> None:
    """Time element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _TIME_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_travel_element_component_family() -> None:
    """Travel element vocabulary is covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _TRAVEL_ELEMENT_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_common_phrase_cooking_recipe_family() -> None:
    """Cooking and recipe common phrases are covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_COOKING_RECIPES_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_landscape_family() -> None:
    """Landscape common phrases are covered except non-HSE-owned drink."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _COMMON_PHRASES_LANDSCAPE_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_common_phrase_annals_status_family() -> None:
    """Annals status common phrases are covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(leaf for leaf in _COMMON_PHRASES_ANNALS_STATUS_LEAVES if not _is_covered(leaf, keys))

    assert missing == []


def test_historyspice_common_covers_common_phrase_transfer_recovery_family() -> None:
    """Transfer and recovery common phrases are covered except non-HSE-owned get."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_TRANSFER_RECOVERY_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_sentiment_descriptor_family() -> None:
    """Sentiment, blessing, temporal, and calm descriptor common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_SENTIMENT_DESCRIPTOR_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_celebration_family() -> None:
    """Celebration common phrases are covered as one HSE component family."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_CELEBRATION_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_conflict_coalition_family() -> None:
    """Challenge, chastisement, and coalition common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_CONFLICT_COALITION_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_creation_gathering_conquest_family() -> None:
    """Creation, gathering, and conquest common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_CREATION_GATHERING_CONQUEST_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_corruption_coronation_family() -> None:
    """Corruption and coronation common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_CORRUPTION_CORONATION_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_defiance_demonstration_family() -> None:
    """Defiance and demonstration common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_DEFIANCE_DEMONSTRATION_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_depravity_despots_family() -> None:
    """Depravity and despot common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_DEPRAVITY_DESPOTS_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_door_emergence_family() -> None:
    """Door, embrace, and emergence common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_DOOR_EMERGENCE_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_enacting_entwined_epic_family() -> None:
    """Enacting, entwined, and epic common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_ENACTING_ENTWINED_EPIC_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_time_family_festival_find_family() -> None:
    """Time, family, festival, fate, and find common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_TIME_FAMILY_FESTIVAL_FIND_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_finesse_foes_family() -> None:
    """Finesse and foes common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf for leaf in _COMMON_PHRASES_FINESSE_FOES_LEAVES if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_people_time_ghost_family() -> None:
    """People, forever/from-then-on, and ghost-title common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PEOPLE_TIME_GHOST_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_gift_grave_greatly_family() -> None:
    """Gift, grave, and greatly common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_GIFT_GRAVE_GREATLY_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_group_hark_harm_family() -> None:
    """Group, hark, and harm common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_GROUP_HARK_HARM_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_hearth_helping_historic_family() -> None:
    """Hearth, helping, and historic common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_HEARTH_HELPING_HISTORIC_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_hold_honoring_horror_family() -> None:
    """Hold, honoring, and horror common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_HOLD_HONORING_HORROR_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_humble_hunter_importance_family() -> None:
    """Humble, hunter, and importance common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_HUMBLE_HUNTER_IMPORTANCE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_in_honor_of_inauguration_inspired_family() -> None:
    """In-honor-of, inauguration, and inspired common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_IN_HONOR_OF_INAUGURATION_INSPIRED_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_interesting_intrepid_family() -> None:
    """Interesting and intrepid common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_INTERESTING_INTREPID_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_discovery_kind_alternatives() -> None:
    """Discovery phrases and kind adjective alternatives are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_DISCOVERY_KIND_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_larvae_laws_family() -> None:
    """Larvae and laws common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LARVAE_LAWS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_learned_family() -> None:
    """Learned and learned-of common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LEARNED_LEARNED_OF_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_learning_listen_family() -> None:
    """Learning and listen common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LEARNING_LISTEN_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_liberated_family() -> None:
    """Liberated common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LIBERATED_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_lost_family() -> None:
    """Lost and lost-in-tavern common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LOST_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_love_lovers_family() -> None:
    """Love and lovers common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LOVE_LOVERS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_luckily_marvel_family() -> None:
    """Luckily and marvel common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_LUCKILY_MARVEL_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_might_misuse_morality_family() -> None:
    """Might, misuse, and morality common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_MIGHT_MISUSE_MORALITY_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_mug_noble_family() -> None:
    """Mug and noble common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_MUG_NOBLE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_object_observe_occasion_family() -> None:
    """Object, observe, and occasion common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_OBJECT_OBSERVE_OCCASION_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_odious_family() -> None:
    """Odious common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_ODIOUS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_onlooker_picks_family() -> None:
    """Onlooker and picks common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_ONLOOKER_PICKS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_pigfarm_plague_plan_family() -> None:
    """Pigfarm, plague, and plan common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PIGFARM_PLAGUE_PLAN_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_practice_pretender_prized_family() -> None:
    """Practice, pretender, and prized common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PRACTICE_PRETENDER_PRIZED_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_profanity_prohibited_family() -> None:
    """Profanity and prohibited common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PROFANITY_PROHIBITED_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_protect_protection_family() -> None:
    """Protect and protection common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PROTECT_PROTECTION_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_puff_ravaged_family() -> None:
    """Puff and ravaged common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PUFF_RAVAGED_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_remember_rife_rituals_family() -> None:
    """Remember, rife, and rituals common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_REMEMBER_RIFE_RITUALS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_conflict_rescue_victory_family() -> None:
    """Conflict, rescue, and victory common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_CONFLICT_RESCUE_VICTORY_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_descriptor_emotion_warning_family() -> None:
    """Descriptor, emotion, and warning common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_DESCRIPTOR_EMOTION_WARNING_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_civic_social_work_family() -> None:
    """Civic, social, and work common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_CIVIC_SOCIAL_WORK_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_value_diplomacy_support_family() -> None:
    """Value, diplomacy, and support common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_VALUE_DIPLOMACY_SUPPORT_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_place_time_family() -> None:
    """Place and time common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_PLACE_TIME_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_discovery_lineage_marking_customs_family() -> None:
    """Discovery, lineage, marking, and customs common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_DISCOVERY_LINEAGE_MARKING_CUSTOMS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_common_phrase_settlement_travel_voice_family() -> None:
    """Settlement, travel, and voice common phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _COMMON_PHRASES_SETTLEMENT_TRAVEL_VOICE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_abdication_protocol_approach_family() -> None:
    """Abdication, protocol, and approach instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_ABDICATION_PROTOCOL_APPROACH_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_bless_maiming_faith_family() -> None:
    """Blessing, maiming, and faith-breaking instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_BLESS_MAIMING_FAITH_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_speech_common_folk_curse_family() -> None:
    """Speech, common-folk, and curse instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_SPEECH_COMMON_FOLK_CURSE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_deadly_liquids_family() -> None:
    """Deadly liquid instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_DEADLY_LIQUIDS_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_dear_ones_desire_family() -> None:
    """Dear-ones and desire instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_DEAR_ONES_DESIRE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_social_fate_movement_family() -> None:
    """Social action, fate, and movement instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_SOCIAL_FATE_MOVEMENT_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_time_forest_place_family() -> None:
    """Time and forest-place instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_TIME_FOREST_PLACE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_punishment_possession_appeal_family() -> None:
    """Punishment, possession, and appeal instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_PUNISHMENT_POSSESSION_APPEAL_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_illness_justice_kinship_proximity_family() -> None:
    """Illness, justice, kinship, and proximity instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_ILLNESS_JUSTICE_KINSHIP_PROXIMITY_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_affirmation_lifesave_murder_time_family() -> None:
    """Affirmation, lifesaving, murder, and time instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_AFFIRMATION_LIFESAVE_MURDER_TIME_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_religion_profanity_recency_reemergence_family() -> None:
    """Religion, profanity, recency, and reemergence instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_RELIGION_PROFANITY_RECENCY_REEMERGENCE_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_reward_royal_secession_place_stepdown_family() -> None:
    """Reward, royal, secession, place, and step-down instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_REWARD_ROYAL_SECESSION_PLACE_STEPDOWN_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_tar_thank_body_tremble_tutor_number_family() -> None:
    """Tar, thanks, body-part, trembling, tutor, and number instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_TAR_THANK_BODY_TREMBLE_TUTOR_NUMBER_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []


def test_historyspice_common_covers_instances_unfortunate_warrior_request_family() -> None:
    """Unfortunate, warrior, request, and exclamation instance phrases are covered."""
    dictionaries_root = _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
    keys = coverage.load_dictionary_keys(
        [
            dictionaries_root / "Scoped" / "historyspice-common.ja.json",
            dictionaries_root / "world-gospels.ja.json",
        ],
    )

    missing = sorted(
        leaf
        for leaf in _INSTANCES_UNFORTUNATE_WARRIOR_REQUEST_LEAVES
        if not _is_covered(leaf, keys)
    )

    assert missing == []
